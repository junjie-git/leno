using Leno.ReviewAfterSales.Domain.Aggregates;
using Leno.ReviewAfterSales.Domain.Repositories;
using Leno.ReviewAfterSales.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Leno.ReviewAfterSales.Infrastructure.Repositories;

/// <summary>
/// 售后单 EF Core 仓储实现。
/// 按订单查询售后单、判断同订单行是否存在进行中同类型售后单、分页条件查询售后单列表。
/// </summary>
public sealed class EfCoreAfterSalesRepository : IAfterSalesRepository
{
    private readonly ReviewAfterSalesDbContext _context;

    public EfCoreAfterSalesRepository(ReviewAfterSalesDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public async Task<AfterSales?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.AfterSales.FirstOrDefaultAsync(a => a.Id == id, ct);

    /// <inheritdoc />
    public async Task<List<AfterSales>> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default)
        => await _context.AfterSales
            .Where(a => a.OrderId == orderId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<bool> HasActiveByOrderLineAsync(Guid orderLineId, AfterSalesType type, CancellationToken ct = default)
    {
        var activeStatuses = new List<AfterSalesStatus>
        {
            AfterSalesStatus.Pending,
            AfterSalesStatus.Approved,
            AfterSalesStatus.Refunding
        };

        return await _context.AfterSales
            .AnyAsync(a => a.OrderLineId == orderLineId && a.Type == type && activeStatuses.Contains(a.Status), ct);
    }

    /// <inheritdoc />
    public async Task<List<AfterSales>> QueryAsync(
        Guid? orderId,
        Guid? userId,
        Guid? sellerId,
        AfterSalesStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = ApplyFilters(_context.AfterSales.AsQueryable(), orderId, userId, sellerId, status);

        return await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<int> CountAsync(
        Guid? orderId,
        Guid? userId,
        Guid? sellerId,
        AfterSalesStatus? status,
        CancellationToken ct = default)
    {
        var query = ApplyFilters(_context.AfterSales.AsQueryable(), orderId, userId, sellerId, status);
        return await query.CountAsync(ct);
    }

    /// <inheritdoc />
    public async Task AddAsync(AfterSales aggregate, CancellationToken ct = default)
        => await _context.AfterSales.AddAsync(aggregate, ct);

    /// <inheritdoc />
    public Task UpdateAsync(AfterSales aggregate, CancellationToken ct = default)
    {
        _context.AfterSales.Update(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(AfterSales aggregate, CancellationToken ct = default)
    {
        _context.AfterSales.Remove(aggregate);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 统一应用查询过滤条件，供 QueryAsync 与 CountAsync 复用。
    /// </summary>
    private static IQueryable<AfterSales> ApplyFilters(
        IQueryable<AfterSales> query,
        Guid? orderId,
        Guid? userId,
        Guid? sellerId,
        AfterSalesStatus? status)
    {
        if (orderId.HasValue)
        {
            query = query.Where(a => a.OrderId == orderId.Value);
        }

        if (userId.HasValue)
        {
            query = query.Where(a => a.UserId == userId.Value);
        }

        if (sellerId.HasValue)
        {
            query = query.Where(a => a.SellerId == sellerId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(a => a.Status == status.Value);
        }

        return query;
    }
}
