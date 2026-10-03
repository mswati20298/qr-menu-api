using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QrMenu.Application.Common.Exceptions;
using QrMenu.Application.Common.Interfaces;
using QrMenu.Application.Subscriptions;
using QrMenu.Domain.Entities;
using QrMenu.Infrastructure.Persistence;

namespace QrMenu.Infrastructure.Services;

public class OnlinePaymentService(
    AppDbContext db,
    IPaymentGateway gateway,
    ISubscriptionService subscriptionService,
    IOptions<SubscriptionSettings> options,
    TimeProvider clock,
    ILogger<OnlinePaymentService> logger) : IOnlinePaymentService
{
    private const string Currency = "INR";

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<CheckoutDto> StartCheckoutAsync(Guid restaurantId, Guid userId, StartCheckoutRequest request, CancellationToken ct = default)
    {
        if (!gateway.IsConfigured)
        {
            throw new ConflictException("Online payments are not set up yet. Please contact support.");
        }

        var restaurant = await db.Restaurants.AsNoTracking().FirstOrDefaultAsync(r => r.Id == restaurantId, ct)
            ?? throw new NotFoundException("Restaurant not found.");

        // The price always comes from the catalog, never from the browser.
        var plan = await db.PricingPlans.AsNoTracking().FirstOrDefaultAsync(p => p.Id == request.PricingPlanId && p.IsActive, ct)
            ?? throw new NotFoundException("This plan is no longer available. Please refresh and choose another.");

        var owner = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId && u.RestaurantId == restaurantId, ct);

        var paymentId = Guid.NewGuid();
        var amountInPaise = (long)decimal.Round(plan.Price * 100, 0, MidpointRounding.AwayFromZero);
        var orderId = await gateway.CreateOrderAsync(amountInPaise, Currency, paymentId.ToString("N"), new Dictionary<string, string>
        {
            ["restaurantId"] = restaurantId.ToString(),
            ["plan"] = plan.Name
        }, ct);

        db.PlanPayments.Add(new PlanPayment
        {
            Id = paymentId,
            RestaurantId = restaurantId,
            PricingPlanId = plan.Id,
            PlanName = plan.Name,
            DurationMonths = plan.DurationMonths,
            Amount = plan.Price,
            Currency = Currency,
            Status = PlanPaymentStatus.Created,
            GatewayOrderId = orderId,
            CreatedAt = Now
        });
        await db.SaveChangesAsync(ct);

        return new CheckoutDto(
            gateway.KeyId, orderId, amountInPaise, Currency, plan.Name, restaurant.Name,
            owner?.Name, owner?.Email, restaurant.WhatsAppNumber);
    }

    public async Task<OwnerPlanDto> ConfirmCheckoutAsync(Guid restaurantId, ConfirmCheckoutRequest request, CancellationToken ct = default)
    {
        if (!gateway.IsPaymentSignatureValid(request.RazorpayOrderId, request.RazorpayPaymentId, request.RazorpaySignature))
        {
            logger.LogWarning("Rejected payment confirmation with a bad signature for order {OrderId}", request.RazorpayOrderId);
            throw new ConflictException("We could not verify this payment. If money was deducted, it will be confirmed automatically or refunded.");
        }

        // The order must belong to this restaurant: one owner can never apply another restaurant's payment.
        var exists = await db.PlanPayments.AnyAsync(
            p => p.GatewayOrderId == request.RazorpayOrderId && p.RestaurantId == restaurantId, ct);
        if (!exists)
        {
            throw new NotFoundException("Payment not found.");
        }

        await MarkPaidAsync(request.RazorpayOrderId, request.RazorpayPaymentId, null, ct);
        return await subscriptionService.GetOwnerPlanAsync(restaurantId, ct);
    }

    public async Task<bool> HandleWebhookAsync(string body, string? signature, CancellationToken ct = default)
    {
        if (!gateway.IsWebhookSignatureValid(body, signature))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var eventName = root.GetProperty("event").GetString();
            if (eventName is not ("payment.captured" or "order.paid"))
            {
                return true;
            }

            var payment = root.GetProperty("payload").GetProperty("payment").GetProperty("entity");
            var orderId = payment.GetProperty("order_id").GetString();
            var paymentId = payment.GetProperty("id").GetString();
            var amount = payment.GetProperty("amount").GetInt64();

            if (!string.IsNullOrEmpty(orderId) && !string.IsNullOrEmpty(paymentId))
            {
                await MarkPaidAsync(orderId, paymentId, amount, ct);
            }
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            logger.LogWarning(ex, "Ignored a Razorpay webhook that could not be read");
        }

        return true;
    }

    /// <summary>
    /// Applies the plan once. Both the browser callback and the webhook end up here, so a payment that was
    /// already applied is skipped.
    /// </summary>
    private async Task MarkPaidAsync(string orderId, string paymentId, long? paidAmountInPaise, CancellationToken ct)
    {
        var payment = await db.PlanPayments.AsNoTracking().FirstOrDefaultAsync(p => p.GatewayOrderId == orderId, ct);
        if (payment is null)
        {
            logger.LogWarning("Payment for unknown order {OrderId}", orderId);
            return;
        }

        if (payment.Status == PlanPaymentStatus.Paid)
        {
            return;
        }

        var expected = (long)decimal.Round(payment.Amount * 100, 0, MidpointRounding.AwayFromZero);
        if (paidAmountInPaise.HasValue && paidAmountInPaise.Value != expected)
        {
            logger.LogError("Order {OrderId} was paid {Paid} paise but {Expected} was expected; plan not applied", orderId, paidAmountInPaise, expected);
            return;
        }

        var now = Now;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        // Claim the payment atomically: if the webhook and the browser callback arrive together,
        // only one of them gets a row back, so the plan is never applied twice.
        var claimed = await db.PlanPayments
            .Where(p => p.Id == payment.Id && p.Status == PlanPaymentStatus.Created)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Status, PlanPaymentStatus.Paid)
                .SetProperty(p => p.GatewayPaymentId, paymentId)
                .SetProperty(p => p.PaidAt, now), ct);
        if (claimed == 0)
        {
            return;
        }

        var restaurant = await db.Restaurants.FirstAsync(r => r.Id == payment.RestaurantId, ct);

        // Uses the snapshot taken at checkout (name, months, amount), not the plan's current values.
        var snapshot = new PricingPlan { Id = payment.PricingPlanId, Name = payment.PlanName };
        PlanChanges.ApplyPaid(
            db, restaurant, snapshot, payment.DurationMonths, payment.Amount, PaymentMethod.Online,
            paymentId, null, "owner (online)", options.Value.GraceDays, now);

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        logger.LogInformation("Online payment {PaymentId} applied {Plan} to restaurant {RestaurantId}", paymentId, payment.PlanName, restaurant.Id);
    }
}
