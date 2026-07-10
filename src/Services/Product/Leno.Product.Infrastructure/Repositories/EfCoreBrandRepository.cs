using Leno.Product.Domain.Aggregates;
using Leno.Product.Domain.Repositories;
using Leno.Product.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Leno.Product.Infrastructure.Repositories;

/// <summary>
/// 品牌仓储 EF Core 实现。
/// <see cref="QueryAsync"/> 为只读分页，支持状态过滤与名称关键词模糊匹配。
/// </summary>
public sealed class EfCoreBrandRepository : IBrandRepository
{
    private readonly ProductDbContext _context;

    public EfCoreBrandRepository(ProductDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public Task<Brand?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _context.Brands.FirstOrDefaultAsync(b => b.Id == id, ct);

    /// <inheritdoc />
    public async Task<(IReadOnlyList<Brand> Items, int Total)> QueryAsync(
        BrandStatus? status = null,
        string? keyword = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = _context.Brands.AsNoTracking();

        if (status.HasValue)
        {
            query = query.Where(b => b.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            query = query.Where(b => EF.Functions.Like(b.Name, $"%{kw}%"));
        }

        var total = await query.CountAsync(ct);
        var safePage = page < 1 ? 1 : page;
        var safePageSize = pageSize is <= 0 or > 100 ? 20 : pageSize;

        var items = await query
            .OrderByDescending(b => b.CreatedAt)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    /// <inheritdoc />
    public Task AddAsync(Brand aggregate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        _context.Brands.Add(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UpdateAsync(Brand aggregate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        if (_context.Entry(aggregate).State == EntityState.Detached)
        {
            _context.Brands.Attach(aggregate);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(Brand aggregate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        _context.Brands.Remove(aggregate);
        return Task.CompletedTask;
    }
}
