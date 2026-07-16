using Leno.Order.Domain.Aggregates;
using Leno.Order.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Leno.Order.Infrastructure.Repositories;

/// <summary>
/// 库存预占回滚补偿 EF Core 仓储实现（T18）。
/// </summary>
public sealed class EfCoreStockReservationCompensationRepository : IStockReservationCompensationRepository
{
    private readonly OrderDbContext _context;

    public EfCoreStockReservationCompensationRepository(OrderDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public Task<StockReservationCompensation?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _context.StockReservationCompensations.FirstOrDefaultAsync(c => c.Id == id, ct);

    /// <inheritdoc />
    public async Task AddAsync(StockReservationCompensation compensation, CancellationToken ct = default)
        => await _context.StockReservationCompensations.AddAsync(compensation, ct);

    /// <inheritdoc />
    public Task UpdateAsync(StockReservationCompensation compensation, CancellationToken ct = default)
    {
        _context.StockReservationCompensations.Update(compensation);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<List<StockReservationCompensation>> GetPendingAsync(int batchSize, CancellationToken ct = default)
    {
        var size = batchSize > 0 ? batchSize : 50;
        return _context.StockReservationCompensations
            .Where(c => c.Status == CompensationStatus.Pending)
            .OrderBy(c => c.CreatedAt)
            .Take(size)
            .ToListAsync(ct);
    }
}
