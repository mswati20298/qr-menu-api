using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QrMenu.Application.Common.Exceptions;
using QrMenu.Application.Orders;
using QrMenu.Application.Subscriptions;
using QrMenu.Domain.Entities;
using QrMenu.Infrastructure.Persistence;

namespace QrMenu.Infrastructure.Services;

public class OrderService(AppDbContext db, IOptions<SubscriptionSettings> subscriptionOptions) : IOrderService
{
    public async Task<OrderDto> CreatePublicOrderAsync(string slug, CreateOrderRequest request, CancellationToken ct = default)
    {
        var restaurant = await db.Restaurants.FirstOrDefaultAsync(r => r.Slug == slug && r.IsActive, ct)
            ?? throw new NotFoundException("Restaurant not found.");

        // Plan expired past its grace period, or cancelled: the menu stays visible but ordering is off.
        if (!SubscriptionRules.CanTakeOrders(restaurant, subscriptionOptions.Value.GraceDays, DateTime.UtcNow))
        {
            throw new ForbiddenException(
                "Online ordering is not available for this restaurant right now. Please ask the staff to take your order.",
                "ordering_unavailable");
        }

        var menuItemIds = request.Items.Select(i => i.MenuItemId).Distinct().ToList();
        var menuItems = await db.MenuItems
            .Include(i => i.Category)
            .Include(i => i.Variants)
            .Include(i => i.AddOns)
            .Where(i => menuItemIds.Contains(i.Id) && i.Category.RestaurantId == restaurant.Id)
            .ToListAsync(ct);

        var orderItems = new List<OrderItem>();
        decimal subtotal = 0;

        foreach (var line in request.Items)
        {
            var menuItem = menuItems.FirstOrDefault(m => m.Id == line.MenuItemId)
                ?? throw new NotFoundException("One or more menu items could not be found.");

            if (!menuItem.IsAvailable)
            {
                throw new ConflictException($"\"{menuItem.Name}\" is currently sold out.");
            }

            string? variantName = null;
            var unitPrice = menuItem.Price;

            if (line.VariantId.HasValue)
            {
                var variant = menuItem.Variants.FirstOrDefault(v => v.Id == line.VariantId.Value)
                    ?? throw new NotFoundException("Selected size/variant is no longer available.");
                variantName = variant.Name;
                unitPrice = variant.Price;
            }
            else if (menuItem.Variants.Count > 0)
            {
                throw new ConflictException($"\"{menuItem.Name}\" requires a size to be selected.");
            }

            var selectedAddOns = new List<OrderItemAddOnDto>();
            if (line.AddOnIds is { Count: > 0 })
            {
                foreach (var addOnId in line.AddOnIds)
                {
                    var addOn = menuItem.AddOns.FirstOrDefault(a => a.Id == addOnId)
                        ?? throw new NotFoundException("Selected add-on is no longer available.");
                    selectedAddOns.Add(new OrderItemAddOnDto(addOn.Name, addOn.Price));
                }
            }

            var addOnsTotal = selectedAddOns.Sum(a => a.Price);
            var lineTotal = (unitPrice + addOnsTotal) * line.Qty;
            subtotal += lineTotal;

            orderItems.Add(new OrderItem
            {
                Id = Guid.NewGuid(),
                MenuItemId = menuItem.Id,
                ItemName = menuItem.Name,
                VariantName = variantName,
                UnitPrice = unitPrice,
                Qty = line.Qty,
                AddOnsJson = selectedAddOns.Count > 0 ? JsonSerializer.Serialize(selectedAddOns) : null,
                LineTotal = lineTotal
            });
        }

        var serviceChargeAmount = restaurant.IsServiceChargeEnabled && !request.SkipServiceCharge
            ? Math.Round(subtotal * restaurant.ServiceChargePercentage / 100m, 2)
            : 0;
        var gstAmount = restaurant.IsGstEnabled
            ? Math.Round((subtotal + serviceChargeAmount) * restaurant.GstPercentage / 100m, 2)
            : 0;
        var total = subtotal + serviceChargeAmount + gstAmount;

        Table? table = null;
        if (!string.IsNullOrWhiteSpace(request.TableNumber))
        {
            table = await db.Tables.FirstOrDefaultAsync(t => t.RestaurantId == restaurant.Id && t.Number == request.TableNumber, ct);
        }

        var order = new Order
        {
            Id = Guid.NewGuid(),
            RestaurantId = restaurant.Id,
            TableId = table?.Id,
            TableNumberSnapshot = request.TableNumber,
            CustomerName = request.CustomerName,
            CustomerPhone = request.CustomerPhone,
            Note = request.Note,
            Status = OrderStatus.Placed,
            Subtotal = subtotal,
            ServiceChargeAmount = serviceChargeAmount,
            GstAmount = gstAmount,
            Total = total,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Items = orderItems
        };

        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);

        return ToDto(order);
    }

    public async Task<List<OrderDto>> GetOrdersByPhoneAsync(string slug, string phone, CancellationToken ct = default)
    {
        var restaurant = await db.Restaurants.FirstOrDefaultAsync(r => r.Slug == slug, ct)
            ?? throw new NotFoundException("Restaurant not found.");

        var orders = await db.Orders
            .Include(o => o.Items)
            .Where(o => o.RestaurantId == restaurant.Id && o.CustomerPhone == phone)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(ct);

        return orders.Select(ToDto).ToList();
    }

    public async Task<OrderDto> GetPublicOrderAsync(string slug, Guid orderId, CancellationToken ct = default)
    {
        var restaurant = await db.Restaurants.FirstOrDefaultAsync(r => r.Slug == slug, ct)
            ?? throw new NotFoundException("Restaurant not found.");

        var order = await db.Orders.Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.RestaurantId == restaurant.Id, ct)
            ?? throw new NotFoundException("Order not found.");

        return ToDto(order);
    }

    public async Task<OrderDto> CancelPublicOrderAsync(string slug, Guid orderId, CancellationToken ct = default)
    {
        var restaurant = await db.Restaurants.FirstOrDefaultAsync(r => r.Slug == slug, ct)
            ?? throw new NotFoundException("Restaurant not found.");

        var order = await db.Orders.Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.RestaurantId == restaurant.Id, ct)
            ?? throw new NotFoundException("Order not found.");

        if (order.Status == OrderStatus.Cancelled)
        {
            throw new ConflictException("This order has already been cancelled.");
        }

        if (order.Status != OrderStatus.Placed)
        {
            throw new ConflictException("This order can no longer be cancelled — the kitchen has already started on it. Please contact the restaurant directly.");
        }

        order.Status = OrderStatus.Cancelled;
        order.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return ToDto(order);
    }

    public async Task<OrderDto> ClaimPublicPaymentAsync(string slug, Guid orderId, ClaimPaymentRequest request, CancellationToken ct = default)
    {
        var restaurant = await db.Restaurants.FirstOrDefaultAsync(r => r.Slug == slug && r.IsActive, ct)
            ?? throw new NotFoundException("Restaurant not found.");

        if (string.IsNullOrWhiteSpace(restaurant.UpiId))
        {
            throw new ConflictException("This restaurant does not take UPI payments online. Please pay at the counter.");
        }

        var order = await db.Orders.Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.RestaurantId == restaurant.Id, ct)
            ?? throw new NotFoundException("Order not found.");

        if (order.Status == OrderStatus.Cancelled)
        {
            throw new ConflictException("This order was cancelled, so there is nothing to pay.");
        }

        if (order.PaymentStatus == OrderPaymentStatus.Paid)
        {
            throw new ConflictException("This order is already paid.");
        }

        // A second claim (e.g. to add the transaction id) just updates the first one.
        order.PaymentStatus = OrderPaymentStatus.Claimed;
        order.PaymentReference = string.IsNullOrWhiteSpace(request.Reference) ? order.PaymentReference : request.Reference.Trim();
        order.PaymentClaimedAt = DateTime.UtcNow;
        order.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return ToDto(order);
    }

    public async Task<OrderDto> UpdatePaymentAsync(Guid restaurantId, Guid orderId, UpdatePaymentRequest request, CancellationToken ct = default)
    {
        var order = await GetOwnedOrderAsync(restaurantId, orderId, ct);

        if (request.Status == "Paid")
        {
            order.PaymentStatus = OrderPaymentStatus.Paid;
            order.PaymentMethod = Enum.Parse<PaymentMethod>(request.Method!, ignoreCase: true);
            order.PaidAt = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(request.Reference))
            {
                order.PaymentReference = request.Reference.Trim();
            }
        }
        else
        {
            // Claim rejected (money not received) or a payment marked by mistake.
            order.PaymentStatus = OrderPaymentStatus.Unpaid;
            order.PaymentMethod = null;
            order.PaidAt = null;
            order.PaymentClaimedAt = null;
            order.PaymentReference = null;
        }

        order.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToDto(order);
    }

    public async Task<List<OrderDto>> GetAllForOwnerAsync(Guid restaurantId, string? status, CancellationToken ct = default)
    {
        var query = db.Orders.Include(o => o.Items).Include(o => o.Invoice).Where(o => o.RestaurantId == restaurantId);

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<OrderStatus>(status, true, out var parsedStatus))
        {
            query = query.Where(o => o.Status == parsedStatus);
        }

        var orders = await query.OrderByDescending(o => o.CreatedAt).ToListAsync(ct);
        return orders.Select(ToDto).ToList();
    }

    public async Task<List<OrderDto>> GetNewOrdersSinceAsync(Guid restaurantId, DateTime since, CancellationToken ct = default)
    {
        var orders = await db.Orders
            .Include(o => o.Items)
            .Include(o => o.Invoice)
            .Where(o => o.RestaurantId == restaurantId && o.CreatedAt > since)
            .OrderBy(o => o.CreatedAt)
            .ToListAsync(ct);

        return orders.Select(ToDto).ToList();
    }

    public async Task<OrderDto> GetForOwnerAsync(Guid restaurantId, Guid orderId, CancellationToken ct = default)
    {
        var order = await GetOwnedOrderAsync(restaurantId, orderId, ct);
        return ToDto(order);
    }

    public async Task<OrderDto> UpdateStatusAsync(Guid restaurantId, Guid orderId, string status, CancellationToken ct = default)
    {
        var order = await GetOwnedOrderAsync(restaurantId, orderId, ct);
        var newStatus = Enum.Parse<OrderStatus>(status, true);
        if (newStatus == OrderStatus.Cancelled && order.InvoiceId.HasValue)
        {
            throw new ConflictException("This order is already on an invoice, so it cannot be cancelled.");
        }

        order.Status = newStatus;
        order.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToDto(order);
    }

    private async Task<Order> GetOwnedOrderAsync(Guid restaurantId, Guid orderId, CancellationToken ct)
    {
        var order = await db.Orders.Include(o => o.Items).Include(o => o.Invoice).FirstOrDefaultAsync(o => o.Id == orderId, ct)
            ?? throw new NotFoundException("Order not found.");

        if (order.RestaurantId != restaurantId)
        {
            throw new NotFoundException("Order not found.");
        }

        return order;
    }

    private static OrderDto ToDto(Order o) => new(
        o.Id, o.TableNumberSnapshot, o.CustomerName, o.CustomerPhone, o.Note, o.Status.ToString(),
        o.Subtotal, o.ServiceChargeAmount, o.GstAmount, o.Total, o.CreatedAt,
        o.Items.Select(i => new OrderItemDto(
            i.Id, i.ItemName, i.VariantName, i.UnitPrice, i.Qty,
            string.IsNullOrEmpty(i.AddOnsJson)
                ? []
                : JsonSerializer.Deserialize<List<OrderItemAddOnDto>>(i.AddOnsJson) ?? [],
            i.LineTotal
        )).ToList(),
        o.PaymentStatus.ToString(), o.PaymentReference, o.PaymentMethod?.ToString(), o.PaidAt,
        o.InvoiceId, o.Invoice?.Number);
}
