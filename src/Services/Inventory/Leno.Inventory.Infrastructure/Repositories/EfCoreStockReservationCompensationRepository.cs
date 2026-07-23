using Leno.Inventory.Domain.Aggregates;
using Leno.Inventory.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Leno.Inventory.Infrastructure.Repositories;

/// <summary>
/// 库存预占回滚补偿 EF Core 仓储实现（T18），迁移自 Order BC。
/// 分页查询待重试（Pending）补偿记录，按创建时间升序（先入先重试）。
/// </summary>
public sealed class EfCoreStockReservationCompensationRepository : IStockReservationCompensationRepository
{
    private readonly InventoryDbContext _context;

    public EfCoreStockReservationCompensationRepository(InventoryDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public Task<StockReservationCompensation?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _context.StockReservationCompensations.FirstOrDefaultAsync(c => c.Id == id, ct);

    /// <inheritdoc />
    public async Task AddAsync(StockReservationCompensation compensation, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(compensation);
        await _context.StockReservationCompensations.AddAsync(compensation, ct);
    }

    /// <inheritdoc />
    public Task UpdateAsync(StockReservationCompensation compensation, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(compensation);
        if (_context.Entry(compensation).State == EntityState.Detached)
        {
            _context.StockReservationCompensations.Attach(compensation);
        }

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
