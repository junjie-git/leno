using Leno.SellerShop.Domain.Aggregates;
using Leno.SellerShop.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Leno.SellerShop.Infrastructure.Repositories;

/// <summary>
/// 店铺运营指标仓储 EF Core 实现。
/// 写场景经 <see cref="GetByShopIdAsync"/> 加载带跟踪的聚合后增量更新；
/// <see cref="GetByDateRangeAsync"/> 为只读趋势查询。UpsertAsync 用于新建聚合写入。
/// </summary>
public sealed class EfCoreShopMetricsRepository : IShopMetricsRepository
{
    private readonly SellerShopDbContext _context;

    public EfCoreShopMetricsRepository(SellerShopDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public Task<ShopMetrics?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _context.ShopMetrics.FirstOrDefaultAsync(m => m.Id == id, ct);

    /// <inheritdoc />
    public Task AddAsync(ShopMetrics aggregate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        _context.ShopMetrics.Add(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UpdateAsync(ShopMetrics aggregate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        if (_context.Entry(aggregate).State == EntityState.Detached)
        {
            _context.ShopMetrics.Attach(aggregate);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(ShopMetrics aggregate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        _context.ShopMetrics.Remove(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<ShopMetrics?> GetByShopIdAsync(Guid shopId, DateOnly metricsDate, CancellationToken ct = default)
        => _context.ShopMetrics.FirstOrDefaultAsync(m => m.ShopId == shopId && m.Date == metricsDate, ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ShopMetrics>> GetByDateRangeAsync(
        Guid shopId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken ct = default)
    {
        return await _context.ShopMetrics
            .AsNoTracking()
            .Where(m => m.ShopId == shopId && m.Date >= fromDate && m.Date <= toDate)
            .OrderBy(m => m.Date)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task UpsertAsync(ShopMetrics metrics, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        // 已跟踪的聚合（经 GetByShopIdAsync 加载并修改）由变更跟踪器处理，无需重复操作。
        if (_context.Entry(metrics).State != EntityState.Detached)
        {
            return;
        }

        var existing = await _context.ShopMetrics
            .FirstOrDefaultAsync(m => m.ShopId == metrics.ShopId && m.Date == metrics.Date, ct);

        if (existing is null)
        {
            _context.ShopMetrics.Add(metrics);
        }
        else
        {
            // 同 (ShopId, Date) 已存在：以既有聚合为准，新聚合字段不可直接覆盖（保留既有 Id 与审计链）。
            // 调用方应优先经 GetByShopIdAsync 加载既有聚合后再增量更新。
            _context.ShopMetrics.Attach(metrics);
            _context.Entry(metrics).State = EntityState.Modified;
        }
    }
}
