using Leno.Payment.Domain.Aggregates;
using Leno.Payment.Domain.Repositories;
using Leno.Payment.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Leno.Payment.Infrastructure.Repositories;

/// <summary>
/// 对账差异 EF Core 仓储实现。
/// </summary>
public sealed class EfCoreReconciliationDiffRepository : IReconciliationDiffRepository
{
    private readonly PaymentDbContext _context;

    public EfCoreReconciliationDiffRepository(PaymentDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public async Task<ReconciliationDiff?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.ReconciliationDiffs.FindAsync([id], ct);

    /// <inheritdoc />
    public async Task<List<ReconciliationDiff>> QueryAsync(
        DateTime? billDate,
        PaymentChannel? channel,
        ReconciliationDiffType? diffType,
        ReconciliationDiffStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _context.ReconciliationDiffs.AsQueryable();

        if (billDate.HasValue)
            query = query.Where(d => d.BillDate == billDate.Value.Date);
        if (channel.HasValue)
            query = query.Where(d => d.Channel == channel.Value);
        if (diffType.HasValue)
            query = query.Where(d => d.DiffType == diffType.Value);
        if (status.HasValue)
            query = query.Where(d => d.Status == status.Value);

        return await query
            .OrderByDescending(d => d.BillDate)
            .ThenBy(d => d.Channel)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<int> CountAsync(
        DateTime? billDate,
        PaymentChannel? channel,
        ReconciliationDiffType? diffType,
        ReconciliationDiffStatus? status,
        CancellationToken ct = default)
    {
        var query = _context.ReconciliationDiffs.AsQueryable();

        if (billDate.HasValue)
            query = query.Where(d => d.BillDate == billDate.Value.Date);
        if (channel.HasValue)
            query = query.Where(d => d.Channel == channel.Value);
        if (diffType.HasValue)
            query = query.Where(d => d.DiffType == diffType.Value);
        if (status.HasValue)
            query = query.Where(d => d.Status == status.Value);

        return await query.CountAsync(ct);
    }

    /// <inheritdoc />
    public async Task AddAsync(ReconciliationDiff aggregate, CancellationToken ct = default)
        => await _context.ReconciliationDiffs.AddAsync(aggregate, ct);

    /// <inheritdoc />
    public Task UpdateAsync(ReconciliationDiff aggregate, CancellationToken ct = default)
    {
        _context.ReconciliationDiffs.Update(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(ReconciliationDiff aggregate, CancellationToken ct = default)
    {
        _context.ReconciliationDiffs.Remove(aggregate);
        return Task.CompletedTask;
    }
}