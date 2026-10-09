using Microsoft.EntityFrameworkCore;
using QrMenu.Application.Common;
using QrMenu.Application.Common.Exceptions;
using QrMenu.Application.Tables;
using QrMenu.Domain.Entities;
using QrMenu.Infrastructure.Persistence;

namespace QrMenu.Infrastructure.Services;

public class TableService(AppDbContext db) : ITableService
{
    private static readonly OrderStatus[] ActiveStatuses = [OrderStatus.Placed, OrderStatus.Preparing, OrderStatus.Served];

    public async Task<List<TableDto>> GetAllAsync(Guid restaurantId, CancellationToken ct = default)
    {
        var tables = await db.Tables
            .Where(t => t.RestaurantId == restaurantId)
            .OrderBy(t => t.Number)
            .ToListAsync(ct);

        var activeTableIds = await db.Orders
            .Where(o => o.RestaurantId == restaurantId && o.TableId != null && ActiveStatuses.Contains(o.Status))
            .Select(o => o.TableId!.Value)
            .Distinct()
            .ToListAsync(ct);

        return tables.Select(t => ToDto(t, activeTableIds.Contains(t.Id))).ToList();
    }

    public async Task<TableDto> CreateAsync(Guid restaurantId, CreateTableRequest request, CancellationToken ct = default)
    {
        var number = NameRules.Normalize(request.Number);
        await DuplicateGuard.TableNumberIsFreeAsync(db, restaurantId, number, null, ct);

        var table = new Table
        {
            Id = Guid.NewGuid(),
            RestaurantId = restaurantId,
            Number = number,
            Capacity = request.Capacity,
            IsActive = true,
            QrCode = TableCodes.New()
        };

        db.Tables.Add(table);
        await db.SaveChangesAsync(ct);

        return ToDto(table, false);
    }

    public async Task<TableDto> UpdateAsync(Guid restaurantId, Guid tableId, UpdateTableRequest request, CancellationToken ct = default)
    {
        var table = await GetOwnedTableAsync(restaurantId, tableId, ct);

        var number = NameRules.Normalize(request.Number);
        await DuplicateGuard.TableNumberIsFreeAsync(db, restaurantId, number, tableId, ct);
        table.Number = number;
        table.Capacity = request.Capacity;
        table.IsActive = request.IsActive;

        await db.SaveChangesAsync(ct);

        var hasActiveOrder = await db.Orders.AnyAsync(o => o.TableId == tableId && ActiveStatuses.Contains(o.Status), ct);
        return ToDto(table, hasActiveOrder);
    }

    public async Task DeleteAsync(Guid restaurantId, Guid tableId, CancellationToken ct = default)
    {
        var table = await GetOwnedTableAsync(restaurantId, tableId, ct);

        var linkedOrders = await db.Orders.Where(o => o.TableId == tableId).ToListAsync(ct);
        foreach (var order in linkedOrders)
        {
            order.TableId = null;
        }

        db.Tables.Remove(table);
        await db.SaveChangesAsync(ct);
    }

    public async Task<TableDto> ResetQrCodeAsync(Guid restaurantId, Guid tableId, CancellationToken ct = default)
    {
        var table = await GetOwnedTableAsync(restaurantId, tableId, ct);
        table.QrCode = TableCodes.New();
        await db.SaveChangesAsync(ct);

        var hasActiveOrder = await db.Orders.AnyAsync(o => o.TableId == tableId && ActiveStatuses.Contains(o.Status), ct);
        return ToDto(table, hasActiveOrder);
    }

    private async Task<Table> GetOwnedTableAsync(Guid restaurantId, Guid tableId, CancellationToken ct)
    {
        var table = await db.Tables.FirstOrDefaultAsync(t => t.Id == tableId, ct)
            ?? throw new NotFoundException("Table not found.");

        if (table.RestaurantId != restaurantId)
        {
            throw new NotFoundException("Table not found.");
        }

        return table;
    }

    private static TableDto ToDto(Table t, bool hasActiveOrder) => new(t.Id, t.Number, t.Capacity, t.IsActive, hasActiveOrder, t.QrCode);
}
