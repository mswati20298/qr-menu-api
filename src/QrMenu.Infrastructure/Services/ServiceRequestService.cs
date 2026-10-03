using Microsoft.EntityFrameworkCore;
using QrMenu.Application.Common.Exceptions;
using QrMenu.Application.ServiceRequests;
using QrMenu.Domain.Entities;
using QrMenu.Infrastructure.Persistence;

namespace QrMenu.Infrastructure.Services;

public class ServiceRequestService(AppDbContext db) : IServiceRequestService
{
    public async Task<ServiceRequestDto> CreatePublicAsync(string slug, CreateServiceRequest request, CancellationToken ct = default)
    {
        var restaurant = await db.Restaurants.FirstOrDefaultAsync(r => r.Slug == slug && r.IsActive, ct)
            ?? throw new NotFoundException("Restaurant not found.");

        // Only real, active tables can raise a request, so a made-up ?t= value can't spam the staff.
        var table = await db.Tables.FirstOrDefaultAsync(
            t => t.RestaurantId == restaurant.Id && t.Number == request.TableNumber && t.IsActive, ct)
            ?? throw new NotFoundException("Table not found.");

        var type = Enum.Parse<ServiceRequestType>(request.Type, ignoreCase: true);

        // Same table already asked for the same thing and nobody has handled it yet: don't stack duplicates.
        var existing = await db.ServiceRequests.FirstOrDefaultAsync(
            r => r.RestaurantId == restaurant.Id
                && r.TableNumber == table.Number
                && r.Type == type
                && r.Status == ServiceRequestStatus.Pending, ct);

        if (existing is not null)
        {
            return ToDto(existing);
        }

        var entity = new ServiceRequest
        {
            Id = Guid.NewGuid(),
            RestaurantId = restaurant.Id,
            TableNumber = table.Number,
            Type = type,
            Status = ServiceRequestStatus.Pending
        };

        db.ServiceRequests.Add(entity);
        await db.SaveChangesAsync(ct);

        return ToDto(entity);
    }

    public async Task<List<ServiceRequestDto>> GetPendingAsync(Guid restaurantId, CancellationToken ct = default)
    {
        var items = await db.ServiceRequests
            .Where(r => r.RestaurantId == restaurantId && r.Status == ServiceRequestStatus.Pending)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(ct);

        return items.Select(ToDto).ToList();
    }

    public async Task CompleteAsync(Guid restaurantId, Guid requestId, CancellationToken ct = default)
    {
        var request = await db.ServiceRequests.FirstOrDefaultAsync(r => r.Id == requestId && r.RestaurantId == restaurantId, ct)
            ?? throw new NotFoundException("Request not found.");

        if (request.Status == ServiceRequestStatus.Pending)
        {
            request.Status = ServiceRequestStatus.Done;
            request.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }

    private static ServiceRequestDto ToDto(ServiceRequest r) =>
        new(r.Id, r.TableNumber, r.Type.ToString(), r.Status.ToString(), r.CreatedAt);
}
