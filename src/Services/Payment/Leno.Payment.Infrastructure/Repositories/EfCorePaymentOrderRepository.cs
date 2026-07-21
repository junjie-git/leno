using Leno.Payment.Domain.Repositories;
using Leno.Payment.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using PaymentOrderAggregate = Leno.Payment.Domain.Aggregates.PaymentOrder;

namespace Leno.Payment.Infrastructure.Repositories;

/// <summary>
/// 支付单 EF Core 仓储实现。
/// </summary>
public sealed class EfCorePaymentOrderRepository : IPaymentOrderRepository
{
    private readonly PaymentDbContext _context;

    public EfCorePaymentOrderRepository(PaymentDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public async Task<PaymentOrderAggregate?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.PaymentOrders
            .FirstOrDefaultAsync(o => o.Id == id, ct);

    /// <inheritdoc />
    public async Task<PaymentOrderAggregate?> GetByOutTradeNoAsync(string outTradeNo, CancellationToken ct = default)
        => await _context.PaymentOrders
            .FirstOrDefaultAsync(o => o.OutTradeNo == outTradeNo, ct);

    /// <inheritdoc />
    public async Task<PaymentOrderAggregate?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default)
        => await _context.PaymentOrders
            .FirstOrDefaultAsync(o => o.OrderId == orderId, ct);

    /// <inheritdoc />
    public async Task<List<PaymentOrderAggregate>> QueryAsync(
        Guid? userId,
        PaymentChannel? channel,
        PaymentStatus? status,
        DateTime? startDate,
        DateTime? endDate,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _context.PaymentOrders.AsQueryable();

        query = ApplyFilters(query, userId, channel, status, startDate, endDate);

        return await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<int> CountAsync(
        Guid? userId,
        PaymentChannel? channel,
        PaymentStatus? status,
        DateTime? startDate,
        DateTime? endDate,
        CancellationToken ct = default)
    {
        var query = _context.PaymentOrders.AsQueryable();

        query = ApplyFilters(query, userId, channel, status, startDate, endDate);

        return await query.CountAsync(ct);
    }

    /// <inheritdoc />
    public async Task<List<PaymentOrderAggregate>> QueryPaidByPaidAtAsync(
        PaymentChannel? channel,
        DateTime paidStart,
        DateTime paidEnd,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _context.PaymentOrders
            .Where(o => o.Status == PaymentStatus.Paid)
            .Where(o => o.PaidAt != null && o.PaidAt >= paidStart && o.PaidAt <= paidEnd);

        if (channel.HasValue)
        {
            query = query.Where(o => o.Channel == channel.Value);
        }

        return await query
            .OrderByDescending(o => o.PaidAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<List<PaymentOrderAggregate>> GetExpiredOrdersAsync(
        DateTime threshold,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _context.PaymentOrders
            .Where(o => o.ExpireAt <= threshold)
            .Where(o => o.Status == PaymentStatus.Pending || o.Status == PaymentStatus.ChannelOrdered);

        return await query
            .OrderBy(o => o.ExpireAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task AddAsync(PaymentOrderAggregate aggregate, CancellationToken ct = default)
        => await _context.PaymentOrders.AddAsync(aggregate, ct);

    /// <inheritdoc />
    public Task UpdateAsync(PaymentOrderAggregate aggregate, CancellationToken ct = default)
    {
        _context.PaymentOrders.Update(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(PaymentOrderAggregate aggregate, CancellationToken ct = default)
    {
        _context.PaymentOrders.Remove(aggregate);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 统一应用查询过滤条件，供 QueryAsync 与 CountAsync 复用。
    /// </summary>
    private static IQueryable<PaymentOrderAggregate> ApplyFilters(
        IQueryable<PaymentOrderAggregate> query,
        Guid? userId,
        PaymentChannel? channel,
        PaymentStatus? status,
        DateTime? startDate,
        DateTime? endDate)
    {
        if (userId.HasValue)
        {
            query = query.Where(o => o.UserId == userId.Value);
        }

        if (channel.HasValue)
        {
            query = query.Where(o => o.Channel == channel.Value);
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
