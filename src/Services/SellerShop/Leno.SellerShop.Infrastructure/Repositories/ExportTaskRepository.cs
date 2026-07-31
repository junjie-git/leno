using Leno.SellerShop.Domain.Aggregates;
using Leno.SellerShop.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Leno.SellerShop.Infrastructure.Repositories;

/// <summary>
/// 导出任务仓储 EF Core 实现。
/// 写场景依赖变更跟踪（GetByIdAsync/UpdateAsync 带跟踪）；ListByShopAsync 为只读分页，使用 AsNoTracking。
/// </summary>
public sealed class ExportTaskRepository : IExportTaskRepository
{
    private readonly SellerShopDbContext _context;

    public ExportTaskRepository(SellerShopDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public Task<ExportTask?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _context.ExportTasks.FirstOrDefaultAsync(t => t.Id == id, ct);

    /// <inheritdoc />
    public Task AddAsync(ExportTask aggregate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        _context.ExportTasks.Add(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UpdateAsync(ExportTask aggregate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        if (_context.Entry(aggregate).State == EntityState.Detached)
        {
            _context.ExportTasks.Attach(aggregate);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(ExportTask aggregate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        _context.ExportTasks.Remove(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<ExportTask> Items, int Total)> ListByShopAsync(
        Guid shopId,
        string? status,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _context.ExportTasks.AsNoTracking().Where(t => t.ShopId == shopId);

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(t => t.Status == status);
        }

        var total = await query.CountAsync(ct);
        var safePage = page < 1 ? 1 : page;
        var safePageSize = pageSize is <= 0 or > 100 ? 20 : pageSize;

        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    /// <inheritdoc />
    public Task<ExportTask?> GetOldestProcessingAsync(CancellationToken ct = default)
        => _context.ExportTasks
            .Where(t => t.Status == "Processing")
            .OrderBy(t => t.CreatedAt)
            .FirstOrDefaultAsync(ct);
}
