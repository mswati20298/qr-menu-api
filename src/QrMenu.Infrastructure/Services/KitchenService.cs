using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QrMenu.Application.Common.Exceptions;
using QrMenu.Application.Common.Interfaces;
using QrMenu.Application.Kitchen;
using QrMenu.Application.Orders;
using QrMenu.Domain.Entities;
using QrMenu.Infrastructure.Auth;
using QrMenu.Infrastructure.Persistence;

namespace QrMenu.Infrastructure.Services;

public class KitchenService(
    AppDbContext db,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService,
    TimeProvider clock) : IKitchenService
{
    // Hash of a random PIN, checked when the restaurant is unknown so every failure takes the same time.
    private const string DummyHash = "$2b$11$QEhrqcyGU9JYdr6rhd9/.OqiE6pNp2swEm1UYbKYSH0CIrj5rane2";

    private static readonly TimeSpan ServedVisibleFor = TimeSpan.FromHours(2);
    private static readonly TimeSpan OpenOrdersFrom = TimeSpan.FromHours(24);

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<KitchenAuthResponse> LoginAsync(KitchenLoginRequest request, CancellationToken ct = default)
    {
        var slug = request.Slug.Trim().ToLowerInvariant();
        var lockKey = $"kitchen:{slug}";

        // A PIN is short, so lock the restaurant's kitchen login after a few wrong tries.
        if (LoginAttemptTracker.IsLocked(lockKey))
        {
            throw new TooManyAttemptsException("Too many wrong PINs. Please try again in a few minutes.");
        }

        var restaurant = await db.Restaurants.FirstOrDefaultAsync(r => r.Slug == slug, ct);
        var pinOk = passwordHasher.Verify(request.Pin, restaurant?.KitchenPinHash ?? DummyHash);

        if (restaurant?.KitchenPinHash is null || !pinOk)
        {
            LoginAttemptTracker.RecordFailure(lockKey);
            throw new UnauthorizedAppException("Wrong restaurant or PIN.");
        }

        if (!restaurant.IsActive)
        {
            throw new ForbiddenException("This restaurant account is suspended. Please contact support.", "restaurant_suspended");
        }

        LoginAttemptTracker.Reset(lockKey);
        return new KitchenAuthResponse(jwtTokenService.GenerateKitchenToken(restaurant), restaurant.Name, restaurant.Slug);
    }

    public async Task<KitchenBoardDto> GetBoardAsync(Guid restaurantId, CancellationToken ct = default)
    {
        var restaurantName = await db.Restaurants.Where(r => r.Id == restaurantId).Select(r => r.Name).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Restaurant not found.");

        var now = Now;
        var openFrom = now - OpenOrdersFrom;
        var servedFrom = now - ServedVisibleFor;

        var orders = await db.Orders.AsNoTracking().Include(o => o.Items)
            .Where(o => o.RestaurantId == restaurantId
                && (((o.Status == OrderStatus.Placed || o.Status == OrderStatus.Preparing) && o.CreatedAt >= openFrom)
                    || (o.Status == OrderStatus.Served && o.UpdatedAt >= servedFrom)))
            .OrderBy(o => o.CreatedAt)
            .Take(200)
            .ToListAsync(ct);

        return new KitchenBoardDto(restaurantName, now, orders.Select(ToDto).ToList());
    }

    public Task<KitchenOrderDto> AdvanceAsync(Guid restaurantId, Guid orderId, CancellationToken ct = default) =>
        MoveAsync(restaurantId, orderId, forward: true, ct);

    public Task<KitchenOrderDto> RevertAsync(Guid restaurantId, Guid orderId, CancellationToken ct = default) =>
        MoveAsync(restaurantId, orderId, forward: false, ct);

    private async Task<KitchenOrderDto> MoveAsync(Guid restaurantId, Guid orderId, bool forward, CancellationToken ct)
    {
        var order = await db.Orders.Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.RestaurantId == restaurantId, ct)
            ?? throw new NotFoundException("Order not found.");

        OrderStatus? next = (forward, order.Status) switch
        {
            (true, OrderStatus.Placed) => OrderStatus.Preparing,
            (true, OrderStatus.Preparing) => OrderStatus.Served,
            (false, OrderStatus.Served) => OrderStatus.Preparing,
            (false, OrderStatus.Preparing) => OrderStatus.Placed,
            _ => null
        };

        if (next is null)
        {
            throw new ConflictException(order.Status == OrderStatus.Cancelled
                ? "This order was cancelled."
                : "This order cannot be moved any further from the kitchen screen.");
        }

        order.Status = next.Value;
        order.UpdatedAt = Now;
        await db.SaveChangesAsync(ct);
        return ToDto(order);
    }

    private static KitchenOrderDto ToDto(Order o) => new(
        o.Id, o.TableNumberSnapshot, o.CustomerName, o.Note, o.Status.ToString(), o.CreatedAt, o.UpdatedAt,
        o.Items.Select(i => new KitchenOrderItemDto(
            i.ItemName,
            i.VariantName,
            string.IsNullOrEmpty(i.AddOnsJson)
                ? []
                : (JsonSerializer.Deserialize<List<OrderItemAddOnDto>>(i.AddOnsJson) ?? []).Select(a => a.Name).ToList(),
            i.Qty)).ToList());
}
