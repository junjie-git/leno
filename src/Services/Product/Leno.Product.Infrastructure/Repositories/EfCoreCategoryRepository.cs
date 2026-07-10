using Leno.Product.Domain.Aggregates;
using Leno.Product.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Leno.Product.Infrastructure.Repositories;

/// <summary>
/// 分类仓储 EF Core 实现。
/// 树查询与子分类查询为只读；写操作由工作单元统一提交。
/// </summary>
public sealed class EfCoreCategoryRepository : ICategoryRepository
{
    private readonly ProductDbContext _context;

    public EfCoreCategoryRepository(ProductDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public Task<Category?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _context.Categories.FirstOrDefaultAsync(c => c.Id == id, ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Category>> GetTreeAsync(CancellationToken ct = default)
    {
        var items = await _context.Categories
            .AsNoTracking()
            .OrderBy(c => c.Level)
            .ThenBy(c => c.SortOrder)
            .ToListAsync(ct);

        return items;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Category>> GetChildrenAsync(Guid parentId, CancellationToken ct = default)
    {
        var items = await _context.Categories
            .AsNoTracking()
            .Where(c => c.ParentId == parentId)
            .OrderBy(c => c.SortOrder)
            .ToListAsync(ct);

        return items;
    }

    /// <inheritdoc />
    public Task<bool> ExistsByNameAsync(string name, Guid? parentId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        return _context.Categories.AsNoTracking().AnyAsync(c => c.Name == name && c.ParentId == parentId, ct);
    }

    /// <inheritdoc />
    public Task AddAsync(Category aggregate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        _context.Categories.Add(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UpdateAsync(Category aggregate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        if (_context.Entry(aggregate).State == EntityState.Detached)
        {
            _context.Categories.Attach(aggregate);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(Category aggregate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        _context.Categories.Remove(aggregate);
        return Task.CompletedTask;
    }
}
