using Leno.Inventory.Domain.Aggregates;
using Leno.Inventory.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Leno.Inventory.Infrastructure.Repositories;

/// <summary>
/// 库存台账仓储 EF Core 实现。
/// 唯一约束 (order_id, sku_id) 由 <see cref="StockReservationConfiguration"/> 声明，
/// 并发/重复预占在数据库层面被拒绝，由应用服务捕获后按幂等重放处理。
/// </summary>
public sealed class EfCoreStockReservationRepository : IStockReservationRepository
{
    private readonly InventoryDbContext _context;

    public EfCoreStockReservationRepository(InventoryDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public Task<StockReservation?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _context.StockReservations.FirstOrDefaultAsync(s => s.Id == id, ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<StockReservation>> GetByOrderAsync(Guid orderId, CancellationToken ct = default)
    {
        var entries = await _context.StockReservations
            .Where(s => s.OrderId == orderId)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        return entries;
    }

    /// <inheritdoc />
    public async Task AddRangeAsync(IReadOnlyList<StockReservation> entries, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entries);
        await _context.StockReservations.AddRangeAsync(entries, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<bool> ExistsForOrderAsync(Guid orderId, CancellationToken ct = default)
        => _context.StockReservations.AnyAsync(s => s.OrderId == orderId, ct);

    /// <inheritdoc />
    public async Task AddAsync(StockReservation aggregate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        await _context.StockReservations.AddAsync(aggregate, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task UpdateAsync(StockReservation aggregate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        if (_context.Entry(aggregate).State == EntityState.Detached)
        {
            _context.StockReservations.Attach(aggregate);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(StockReservation aggregate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        _context.StockReservations.Remove(aggregate);
        return Task.CompletedTask;
    }
}
