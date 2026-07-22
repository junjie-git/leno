using Leno.SellerShop.Domain.Aggregates;
using Leno.SellerShop.Domain.Repositories;
using Leno.SharedKernel.Abstractions;
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
            // 新建聚合：直接 Add，审计拦截器在 SaveChangesAsync 时填充 CreatedAt/CreatedBy/UpdatedAt/UpdatedBy
            _context.ShopMetrics.Add(metrics);
            return;
        }

        // 同 (ShopId, Date) 已存在：以既有聚合为准，仅复制新聚合的业务字段到既有聚合，
        // 保留既有 Id（init 不可变）与审计链（CreatedAt/CreatedBy），避免 EntityState.Modified 直接覆盖。
        // UpdatedAt/UpdatedBy 由审计拦截器在 SaveChangesAsync 时自动刷新。
        // 既有聚合已在 FirstOrDefaultAsync 时被变更跟踪器跟踪，业务字段变更会自动标记为 Modified。
        var existingEntry = _context.Entry(existing);
        var newEntry = _context.Entry(metrics);

        foreach (var property in existingEntry.Metadata.GetProperties())
        {
            // 跳过主键与审计字段，保留既有值（CreatedAt/CreatedBy 不被新聚合的默认值覆盖）
            if (property.IsPrimaryKey()
                || property.Name == nameof(Entity.CreatedAt)
                || property.Name == nameof(Entity.CreatedBy)
                || property.Name == nameof(Entity.UpdatedAt)
                || property.Name == nameof(Entity.UpdatedBy))
            {
                continue;
            }

            existingEntry.Property(property.Name).CurrentValue =
                newEntry.Property(property.Name).CurrentValue;
        }
    }
}
