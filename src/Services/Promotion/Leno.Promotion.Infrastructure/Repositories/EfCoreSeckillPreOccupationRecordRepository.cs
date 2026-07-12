using Leno.Promotion.Domain.Aggregates;
using Leno.Promotion.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Leno.Promotion.Infrastructure.Repositories;

/// <summary>
/// 秒杀预占记录 EF Core 仓储实现。
/// </summary>
public sealed class EfCoreSeckillPreOccupationRecordRepository : ISeckillPreOccupationRecordRepository
{
    private readonly PromotionDbContext _context;

    public EfCoreSeckillPreOccupationRecordRepository(PromotionDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public async Task<SeckillPreOccupationRecord?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.SeckillPreOccupationRecords.FirstOrDefaultAsync(r => r.Id == id, ct);

    /// <inheritdoc />
    public async Task AddAsync(SeckillPreOccupationRecord aggregate, CancellationToken ct = default)
        => await _context.SeckillPreOccupationRecords.AddAsync(aggregate, ct);

    /// <inheritdoc />
    public Task UpdateAsync(SeckillPreOccupationRecord aggregate, CancellationToken ct = default)
    {
        _context.SeckillPreOccupationRecords.Update(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(SeckillPreOccupationRecord aggregate, CancellationToken ct = default)
    {
        _context.SeckillPreOccupationRecords.Remove(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<List<SeckillPreOccupationRecord>> GetUnfulfilledAsync(
        DateTime timeout,
        int skip,
        int take,
        CancellationToken ct = default)
    {
        return await _context.SeckillPreOccupationRecords
            .Where(r => !r.IsFulfilled && !r.IsRolledBack && r.PreOccupiedAt < timeout)
            .OrderBy(r => r.PreOccupiedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<SeckillPreOccupationRecord?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default)
        => await _context.SeckillPreOccupationRecords.FirstOrDefaultAsync(r => r.OrderId == orderId, ct);
}