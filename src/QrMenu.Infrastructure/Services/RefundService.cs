using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QrMenu.Application.Common;
using QrMenu.Application.Common.Exceptions;
using QrMenu.Application.Common.Interfaces;
using QrMenu.Application.Refunds;
using QrMenu.Domain.Entities;
using QrMenu.Infrastructure.Payments;
using QrMenu.Infrastructure.Persistence;

namespace QrMenu.Infrastructure.Services;

/// <summary>
/// Refunds for plan payments. Owners ask (within <see cref="RefundRules.RequestWindowDays"/> days of paying), the super
/// admin approves or rejects, or refunds directly. Online payments go back through Razorpay; payments recorded by hand
/// are given back by hand and recorded here. A full refund stops the plan at once.
/// Razorpay keeps its charges when we refund, so refunds are the payment less those charges (unless the super admin
/// chooses to give them back too, e.g. a double charge). Owners see this before they ask.
/// </summary>
public class RefundService(
    AppDbContext db,
    IPaymentGateway gateway,
    TimeProvider clock,
    ILogger<RefundService> logger,
    PaymentGatewayLogWriter paymentLog) : IRefundService
{
    // Refunds that count against a payment's amount: asked for, on their way, or done.
    private static readonly RefundStatus[] Open = [RefundStatus.Requested, RefundStatus.Processing, RefundStatus.Refunded];
    private static readonly RefundStatus[] MoneyGone = [RefundStatus.Processing, RefundStatus.Refunded];

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    // ---------- Owner ----------

    public async Task<OwnerRefundsDto> GetOwnerRefundsAsync(Guid restaurantId, CancellationToken ct = default)
    {
        var since = Now.AddDays(-RefundRules.RequestWindowDays);
        var paidRecently = await db.PlanPayments.AsNoTracking()
            .Where(p => p.RestaurantId == restaurantId && p.Status == PlanPaymentStatus.Paid && p.PaidAt >= since)
            .Where(p => !db.PlanRefunds.Any(r => r.PlanPaymentId == p.Id && Open.Contains(r.Status)))
            .OrderByDescending(p => p.PaidAt)
            .Select(p => new { p.Id, p.PlanName, p.Amount, PaidAt = p.PaidAt!.Value })
            .ToListAsync(ct);

        // Only payments of the last few days, so asking Razorpay for a missing fee stays rare (and it is saved).
        var refundable = new List<RefundablePaymentDto>();
        foreach (var p in paidRecently)
        {
            var fee = await FeeAsync(p.Id, ct);
            if (p.Amount - fee > 0)
            {
                refundable.Add(new RefundablePaymentDto(p.Id, p.PlanName, p.Amount, fee, p.Amount - fee, p.PaidAt, p.PaidAt.AddDays(RefundRules.RequestWindowDays)));
            }
        }

        var refunds = await Query(r => r.RestaurantId == restaurantId).Take(20).ToListAsync(ct);
        return new OwnerRefundsDto(refundable, refunds.Select(ToDto).ToList());
    }

    public async Task<RefundDto> RequestAsync(Guid restaurantId, RequestRefundRequest request, CancellationToken ct = default)
    {
        var payment = await db.PlanPayments.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.PlanPaymentId && p.RestaurantId == restaurantId, ct)
            ?? throw new NotFoundException("Payment not found.");

        if (payment.Status != PlanPaymentStatus.Paid || payment.PaidAt is null)
        {
            throw new ConflictException("Only a completed payment can be refunded.");
        }
        if (payment.PaidAt.Value.AddDays(RefundRules.RequestWindowDays) < Now)
        {
            throw new ConflictException($"Refunds can be asked for within {RefundRules.RequestWindowDays} days of paying. Please contact support.");
        }
        if (await db.PlanRefunds.AnyAsync(r => r.PlanPaymentId == payment.Id && Open.Contains(r.Status), ct))
        {
            throw new ConflictException("A refund for this payment has already been asked for.");
        }

        var fee = await FeeAsync(payment.Id, ct);
        if (payment.Amount - fee <= 0)
        {
            throw new ConflictException("Nothing is left to refund after the payment charges.");
        }

        var refund = new PlanRefund
        {
            Id = Guid.NewGuid(),
            RestaurantId = restaurantId,
            PlanPaymentId = payment.Id,
            PlanName = payment.PlanName,
            PaymentAmount = payment.Amount,
            Amount = payment.Amount - fee,
            Fee = fee,
            Status = RefundStatus.Requested,
            RequestedBy = "owner",
            Reason = request.Reason.Trim(),
            RequestedAt = Now
        };
        db.PlanRefunds.Add(refund);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Restaurant {RestaurantId} asked for a refund of {Amount} for payment {PaymentId}", restaurantId, refund.Amount, payment.Id);
        return await GetAsync(refund.Id, ct);
    }

    // ---------- Super admin ----------

    public async Task<List<RefundDto>> ListAsync(string? status, CancellationToken ct = default)
    {
        var query = Enum.TryParse<RefundStatus>(status, ignoreCase: true, out var wanted)
            ? Query(r => r.Status == wanted)
            : Query();
        return (await query.Take(300).ToListAsync(ct)).Select(ToDto).ToList();
    }

    public async Task<RefundDto> ApproveAsync(Guid refundId, ApproveRefundRequest request, string performedBy, CancellationToken ct = default)
    {
        var refund = await db.PlanRefunds.FirstOrDefaultAsync(r => r.Id == refundId, ct)
            ?? throw new NotFoundException("Refund not found.");
        if (refund.Status != RefundStatus.Requested)
        {
            throw new ConflictException("This refund has already been handled.");
        }

        var fee = request.IncludeFee ? 0 : await FeeAsync(refund.PlanPaymentId, ct);
        var remaining = refund.PaymentAmount - fee - await RefundedSoFarAsync(refund.PlanPaymentId, refund.PaymentEventId, ct);
        refund.Amount = PickAmount(request.Amount, remaining);
        refund.Fee = fee;
        refund.AdminNote = Clean(request.Note);
        await ExecuteAsync(refund, performedBy, ct);
        return await GetAsync(refund.Id, ct);
    }

    public async Task<RefundDto> RejectAsync(Guid refundId, RejectRefundRequest request, string performedBy, CancellationToken ct = default)
    {
        var refund = await db.PlanRefunds.FirstOrDefaultAsync(r => r.Id == refundId, ct)
            ?? throw new NotFoundException("Refund not found.");
        if (refund.Status != RefundStatus.Requested)
        {
            throw new ConflictException("This refund has already been handled.");
        }

        refund.Status = RefundStatus.Rejected;
        refund.AdminNote = request.Note.Trim();
        refund.DecidedBy = performedBy;
        refund.DecidedAt = Now;
        await db.SaveChangesAsync(ct);
        return await GetAsync(refund.Id, ct);
    }

    public async Task<RefundDto> RefundDirectAsync(AdminRefundRequest request, string performedBy, CancellationToken ct = default)
    {
        PlanRefund refund;
        if (request.PlanPaymentId is { } paymentId)
        {
            var payment = await db.PlanPayments.AsNoTracking().FirstOrDefaultAsync(p => p.Id == paymentId, ct)
                ?? throw new NotFoundException("Payment not found.");
            if (payment.Status != PlanPaymentStatus.Paid)
            {
                throw new ConflictException("This checkout was never paid, so there is nothing to refund.");
            }
            refund = NewAdminRefund(payment.RestaurantId, payment.PlanName, payment.Amount);
            refund.PlanPaymentId = payment.Id;
        }
        else
        {
            var entry = await db.SubscriptionEvents.AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == request.PaymentEventId && e.Action == SubscriptionAction.PaymentRecorded, ct)
                ?? throw new NotFoundException("Payment not found.");
            if (entry.PaymentMethod == PaymentMethod.Online)
            {
                throw new ConflictException("Refund an online payment from its Razorpay row.");
            }
            refund = NewAdminRefund(entry.RestaurantId, entry.PlanName, entry.Amount ?? 0);
            refund.PaymentEventId = entry.Id;
        }

        if (await db.PlanRefunds.AnyAsync(r => r.Status == RefundStatus.Requested
                && (r.PlanPaymentId == refund.PlanPaymentId && r.PlanPaymentId != null), ct))
        {
            throw new ConflictException("The owner already asked for a refund of this payment: approve or reject that request.");
        }

        var fee = request.IncludeFee ? 0 : await FeeAsync(refund.PlanPaymentId, ct);
        var remaining = refund.PaymentAmount - fee - await RefundedSoFarAsync(refund.PlanPaymentId, refund.PaymentEventId, ct);
        refund.Amount = PickAmount(request.Amount, remaining);
        refund.Fee = fee;
        refund.AdminNote = Clean(request.Note);
        db.PlanRefunds.Add(refund);
        await ExecuteAsync(refund, performedBy, ct);
        return await GetAsync(refund.Id, ct);
    }

    public async Task HandleGatewayUpdateAsync(string gatewayRefundId, string gatewayStatus, CancellationToken ct = default)
    {
        var refund = await db.PlanRefunds.FirstOrDefaultAsync(r => r.GatewayRefundId == gatewayRefundId, ct);
        if (refund is null)
        {
            // Refunded in Razorpay's own dashboard: nothing to update here.
            return;
        }

        if (gatewayStatus == "processed" && refund.Status != RefundStatus.Refunded)
        {
            refund.Status = RefundStatus.Refunded;
            refund.RefundedAt = Now;
        }
        else if (gatewayStatus == "failed" && refund.Status != RefundStatus.Failed)
        {
            refund.Status = RefundStatus.Failed;
            logger.LogWarning("Razorpay could not refund {RefundId} for restaurant {RestaurantId}", gatewayRefundId, refund.RestaurantId);
        }
        await db.SaveChangesAsync(ct);
    }

    // ---------- Doing the refund ----------

    /// <summary>Gives the money back (Razorpay, or recorded as done by hand), records it, and stops the plan when it is the full amount.</summary>
    private async Task ExecuteAsync(PlanRefund refund, string performedBy, CancellationToken ct)
    {
        PaymentMethod? method;
        string? reference;

        if (refund.PlanPaymentId is { } paymentId)
        {
            var payment = await db.PlanPayments.AsNoTracking().FirstAsync(p => p.Id == paymentId, ct);
            if (string.IsNullOrEmpty(payment.GatewayPaymentId))
            {
                throw new ConflictException("This payment has no Razorpay payment id, so it cannot be refunded online.");
            }

            // Throws (and nothing is saved) when Razorpay refuses; the request stays open to try again.
            var result = await gateway.RefundAsync(payment.GatewayPaymentId, ToPaise(refund.Amount), refund.Id.ToString("N"), payment.GatewayOrderId, ct);
            refund.GatewayRefundId = result.RefundId;
            refund.Status = result.Status == "processed" ? RefundStatus.Refunded : RefundStatus.Processing;
            refund.RefundedAt = result.Status == "processed" ? Now : null;
            method = PaymentMethod.Online;
            reference = result.RefundId;
        }
        else
        {
            // Paid by hand, given back by hand: this only records it.
            refund.Status = RefundStatus.Refunded;
            refund.RefundedAt = Now;
            var entry = await db.SubscriptionEvents.AsNoTracking().FirstAsync(e => e.Id == refund.PaymentEventId, ct);
            method = entry.PaymentMethod;
            reference = refund.AdminNote;
        }

        refund.DecidedBy = performedBy;
        refund.DecidedAt = Now;

        var restaurant = await db.Restaurants.FirstAsync(r => r.Id == refund.RestaurantId, ct);
        var refundedBefore = await RefundedSoFarAsync(refund.PlanPaymentId, refund.PaymentEventId, ct);
        // Full = nothing left to give back (the charges Razorpay keeps do not count).
        var full = refundedBefore + refund.Amount >= refund.PaymentAmount - refund.Fee;

        var entryRefund = PlanChanges.AddEvent(db, restaurant, SubscriptionAction.Refunded, performedBy,
            full ? "Full refund" : "Partial refund", Now);
        entryRefund.Amount = refund.Amount;
        entryRefund.PaymentMethod = method;
        entryRefund.PaymentReference = reference;

        // A full refund ends the plan straight away (ordering stops), as the owner got all the money back.
        if (full && restaurant.PlanCancelledAt is null)
        {
            restaurant.PlanCancelledAt = Now;
            PlanChanges.AddEvent(db, restaurant, SubscriptionAction.Cancelled, performedBy, "Plan stopped after a full refund", Now);
        }

        await db.SaveChangesAsync(ct);

        if (refund.PlanPaymentId is not null)
        {
            var orderId = await db.PlanPayments.AsNoTracking().Where(p => p.Id == refund.PlanPaymentId).Select(p => p.GatewayOrderId).FirstAsync(ct);
            await paymentLog.WriteAsync("result", orderId, null, null, null,
                $"Refund of ₹{refund.Amount:0.##} {(refund.Status == RefundStatus.Refunded ? "done" : "on its way")} ({refund.GatewayRefundId})"
                + (full ? "; plan stopped (full refund)" : ""));
        }
        logger.LogInformation("Refund {RefundId}: {Amount} for restaurant {RestaurantId} by {By}, full={Full}", refund.Id, refund.Amount, refund.RestaurantId, performedBy, full);
    }

    /// <summary>
    /// Razorpay's charges on an online payment, which it keeps when we refund: read from Razorpay once and saved,
    /// or estimated when Razorpay cannot be asked. Manual payments have none.
    /// </summary>
    private async Task<decimal> FeeAsync(Guid? planPaymentId, CancellationToken ct)
    {
        if (planPaymentId is null)
        {
            return 0;
        }
        var payment = await db.PlanPayments.FirstAsync(p => p.Id == planPaymentId, ct);
        if (payment.GatewayFee is { } known)
        {
            return known;
        }
        if (!string.IsNullOrEmpty(payment.GatewayPaymentId) && await gateway.GetPaymentFeeAsync(payment.GatewayPaymentId, ct) is { } paise)
        {
            payment.GatewayFee = paise / 100m;
            await db.SaveChangesAsync(ct);
            return payment.GatewayFee.Value;
        }
        return RefundRules.EstimateFee(payment.Amount);
    }

    /// <summary>Money already given back (or on its way) for one payment, not counting the refund being worked on.</summary>
    private async Task<decimal> RefundedSoFarAsync(Guid? planPaymentId, Guid? paymentEventId, CancellationToken ct) =>
        await db.PlanRefunds
            .Where(r => MoneyGone.Contains(r.Status)
                && ((planPaymentId != null && r.PlanPaymentId == planPaymentId) || (paymentEventId != null && r.PaymentEventId == paymentEventId)))
            .SumAsync(r => (decimal?)r.Amount, ct) ?? 0;

    private static decimal PickAmount(decimal? wanted, decimal remaining)
    {
        if (remaining <= 0)
        {
            throw new ConflictException("This payment has already been refunded in full (Razorpay keeps its charges).");
        }
        var amount = Math.Round(wanted ?? remaining, 2);
        if (amount > remaining)
        {
            throw new ConflictException($"You can refund at most ₹{remaining:0.##} for this payment.");
        }
        return amount;
    }

    private PlanRefund NewAdminRefund(Guid restaurantId, string? planName, decimal paymentAmount) => new()
    {
        Id = Guid.NewGuid(),
        RestaurantId = restaurantId,
        PlanName = planName,
        PaymentAmount = paymentAmount,
        RequestedBy = "admin",
        RequestedAt = Now
    };

    private static long ToPaise(decimal rupees) => (long)decimal.Round(rupees * 100, 0, MidpointRounding.AwayFromZero);

    private static string? Clean(string? value) => value.CleanOrNull();

    // ---------- Reading ----------

    private sealed record Row(PlanRefund Refund, string RestaurantName, string? GatewayPaymentId);

    /// <summary>Refunds with their restaurant name and Razorpay payment id. Filter first, then this projects.</summary>
    private IQueryable<Row> Query(System.Linq.Expressions.Expression<Func<PlanRefund, bool>>? filter = null) =>
        db.PlanRefunds.AsNoTracking()
            .Where(filter ?? (_ => true))
            .OrderByDescending(r => r.RequestedAt)
            .Select(r => new Row(
                r,
                r.Restaurant.Name,
                db.PlanPayments.Where(p => p.Id == r.PlanPaymentId).Select(p => p.GatewayPaymentId).FirstOrDefault()));

    private async Task<RefundDto> GetAsync(Guid id, CancellationToken ct) =>
        ToDto(await Query(r => r.Id == id).FirstAsync(ct));

    private static RefundDto ToDto(Row row)
    {
        var r = row.Refund;
        return new RefundDto(
            r.Id, r.RestaurantId, row.RestaurantName, r.PlanPaymentId is null ? "manual" : "online", r.PlanName,
            r.PaymentAmount, r.Amount, r.Status.ToString(), r.RequestedBy, r.Reason, r.AdminNote, r.DecidedBy,
            r.GatewayRefundId, row.GatewayPaymentId, r.RequestedAt, r.DecidedAt, r.RefundedAt, r.Fee);
    }
}
