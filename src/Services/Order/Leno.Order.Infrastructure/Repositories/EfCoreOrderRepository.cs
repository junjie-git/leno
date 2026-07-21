using Leno.Order.Domain.Repositories;
using Leno.Order.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using OrderAggregate = Leno.Order.Domain.Aggregates.Order;

namespace Leno.Order.Infrastructure.Repositories;

/// <summary>
/// 订单 EF Core 仓储实现。
/// 读取时一并加载 Items 明细集合，保证聚合内不变量操作完整。
/// </summary>
public sealed class EfCoreOrderRepository : IOrderRepository
{
    private readonly OrderDbContext _context;

    public EfCoreOrderRepository(OrderDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public async Task<OrderAggregate?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id, ct);

    /// <inheritdoc />
    public async Task<OrderAggregate?> GetByOrderNoAsync(string orderNo, CancellationToken ct = default)
        => await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.OrderNo == orderNo, ct);

    /// <inheritdoc />
    public async Task<List<OrderAggregate>> QueryAsync(
        Guid? userId,
        Guid? sellerId,
        OrderStatus? status,
        DateTime? startDate,
        DateTime? endDate,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _context.Orders
            .Include(o => o.Items)
            .AsQueryable();

        query = ApplyFilters(query, userId, sellerId, status, startDate, endDate);

        // P2-T35：page 从 0 起（CQRS 标准），Skip = page * pageSize；负数页码归零保护
        var safePage = page < 0 ? 0 : page;
        return await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip(safePage * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<int> CountAsync(
        Guid? userId,
        Guid? sellerId,
        OrderStatus? status,
        DateTime? startDate,
        DateTime? endDate,
        CancellationToken ct = default)
    {
        var query = _context.Orders.AsQueryable();

        query = ApplyFilters(query, userId, sellerId, status, startDate, endDate);

        return await query.CountAsync(ct);
    }

    /// <inheritdoc />
    public async Task AddAsync(OrderAggregate aggregate, CancellationToken ct = default)
        => await _context.Orders.AddAsync(aggregate, ct);

    /// <inheritdoc />
    public Task UpdateAsync(OrderAggregate aggregate, CancellationToken ct = default)
    {
        _context.Orders.Update(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    /// <remarks>
    /// P1-T26：原实现调用 <c>_context.Orders.Remove(aggregate)</c> 物理删除，会破坏订单历史可追溯性。
    /// 改为调用 <see cref="OrderAggregate.SoftDelete"/> 设置 <c>IsDeleted=true</c> 并发布领域事件，
    /// 由全局查询过滤器 <c>HasQueryFilter(o => !o.IsDeleted)</c> 自动排除已删除记录。
    /// 操作人标识由调用方在调用 <c>SoftDelete(operatorId)</c> 时显式传入；仓储层不感知操作人上下文。
    /// </remarks>
    public Task RemoveAsync(OrderAggregate aggregate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        aggregate.SoftDelete();
        _context.Orders.Update(aggregate);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 统一应用查询过滤条件，供 QueryAsync 与 CountAsync 复用。
    /// </summary>
    private static IQueryable<OrderAggregate> ApplyFilters(
        IQueryable<OrderAggregate> query,
        Guid? userId,
        Guid? sellerId,
        OrderStatus? status,
        DateTime? startDate,
        DateTime? endDate)
    {
        if (userId.HasValue)
        {
            query = query.Where(o => o.UserId == userId.Value);
        }

        if (sellerId.HasValue)
        {
            query = query.Where(o => o.SellerId == sellerId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(o => o.Status == status.Value);
        }

        if (startDate.HasValue)
        {
            query = query.Where(o => o.CreatedAt >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(o => o.CreatedAt <= endDate.Value);
        }

        return query;
    }
}
