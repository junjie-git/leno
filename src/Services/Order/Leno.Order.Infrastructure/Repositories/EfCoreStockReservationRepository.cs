using Leno.Order.Domain.Aggregates;
using Leno.Order.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Leno.Order.Infrastructure.Repositories;

/// <summary>
/// 库存预占聚合 EF Core 仓储实现，提供按 SKU 维度的聚合加载与持久化。
/// 配合 <see cref="RedisInventoryRepository"/> 双写策略，使 DB 成为聚合审计/对账源。
/// </summary>
public sealed class EfCoreStockReservationRepository : IStockReservationRepository
{
    private readonly OrderDbContext _context;

    public EfCoreStockReservationRepository(OrderDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public Task<StockReservation?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _context.StockReservations.FirstOrDefaultAsync(s => s.Id == id, ct);

    /// <inheritdoc />
    public Task<StockReservation?> GetBySkuIdAsync(Guid skuId, CancellationToken ct = default)
        => _context.StockReservations.FirstOrDefaultAsync(s => s.SkuId == skuId, ct);

    /// <inheritdoc />
    public async Task<StockReservation> GetOrCreateAsync(Guid skuId, CancellationToken ct = default)
    {
        var existing = await _context.StockReservations.FirstOrDefaultAsync(s => s.SkuId == skuId, ct);
        if (existing is not null)
        {
            return existing;
        }

        // 创建基线为 0 的新聚合，待 SetBaseLineAsync 同步基线后才能正确执行 ReserveStock
        var reservation = StockReservation.Create(Guid.NewGuid(), skuId, 0);
        await _context.StockReservations.AddAsync(reservation, ct);
        return reservation;
    }

    /// <inheritdoc />
    public async Task AddAsync(StockReservation aggregate, CancellationToken ct = default)
        => await _context.StockReservations.AddAsync(aggregate, ct);

    /// <inheritdoc />
    public Task UpdateAsync(StockReservation aggregate, CancellationToken ct = default)
    {
        _context.StockReservations.Update(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(StockReservation aggregate, CancellationToken ct = default)
    {
        _context.StockReservations.Remove(aggregate);
        return Task.CompletedTask;
    }
}
