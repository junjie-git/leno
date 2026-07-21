using Leno.Payment.Domain.Repositories;
using Leno.Payment.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using RefundOrderAggregate = Leno.Payment.Domain.Aggregates.RefundOrder;

namespace Leno.Payment.Infrastructure.Repositories;

/// <summary>
/// 退款单 EF Core 仓储实现。
/// </summary>
public sealed class EfCoreRefundOrderRepository : IRefundOrderRepository
{
    private readonly PaymentDbContext _context;

    public EfCoreRefundOrderRepository(PaymentDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public async Task<RefundOrderAggregate?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.RefundOrders
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    /// <inheritdoc />
    public async Task<RefundOrderAggregate?> GetByOutRefundNoAsync(string outRefundNo, CancellationToken ct = default)
        => await _context.RefundOrders
            .FirstOrDefaultAsync(r => r.OutRefundNo == outRefundNo, ct);

    /// <inheritdoc />
    public async Task<RefundOrderAggregate?> GetByAfterSalesIdAsync(Guid afterSalesId, CancellationToken ct = default)
        => await _context.RefundOrders
            .FirstOrDefaultAsync(r => r.AfterSalesId == afterSalesId, ct);

    /// <inheritdoc />
    public async Task<RefundOrderAggregate?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default)
        => await _context.RefundOrders
            .FirstOrDefaultAsync(r => r.OrderId == orderId, ct);

    /// <inheritdoc />
    public async Task<List<RefundOrderAggregate>> QueryAsync(
        Guid? orderId,
        RefundStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _context.RefundOrders.AsQueryable();

        query = ApplyFilters(query, orderId, status);

        return await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<int> CountAsync(Guid? orderId, RefundStatus? status, CancellationToken ct = default)
    {
        var query = _context.RefundOrders.AsQueryable();

        query = ApplyFilters(query, orderId, status);

        return await query.CountAsync(ct);
    }

    /// <inheritdoc />
    public async Task<List<RefundOrderAggregate>> GetSuccessfulRefundsByPaymentIdAsync(
        Guid paymentId,
        CancellationToken ct = default)
    {
        return await _context.RefundOrders
            .Where(r => r.PaymentId == paymentId && r.Status == RefundStatus.Succeeded)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task AddAsync(RefundOrderAggregate aggregate, CancellationToken ct = default)
        => await _context.RefundOrders.AddAsync(aggregate, ct);

    /// <inheritdoc />
    public Task UpdateAsync(RefundOrderAggregate aggregate, CancellationToken ct = default)
    {
        _context.RefundOrders.Update(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(RefundOrderAggregate aggregate, CancellationToken ct = default)
    {
        _context.RefundOrders.Remove(aggregate);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 统一应用查询过滤条件，供 QueryAsync 与 CountAsync 复用。
    /// </summary>
    private static IQueryable<RefundOrderAggregate> ApplyFilters(
        IQueryable<RefundOrderAggregate> query,
        Guid? orderId,
        RefundStatus? status)
    {
        if (orderId.HasValue)
        {
            query = query.Where(r => r.OrderId == orderId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(r => r.Status == status.Value);
        }

        return query;
    }
}
