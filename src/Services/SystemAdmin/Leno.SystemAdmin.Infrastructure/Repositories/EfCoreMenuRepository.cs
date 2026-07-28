using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Leno.SystemAdmin.Infrastructure.Repositories;

/// <summary>
/// 菜单聚合根 EF Core 仓储实现。
/// GetByRoleAsync 采用应用层过滤（菜单总数 ≤ 100，避免 LIKE 子串误匹配）；
/// DeleteAsync 通过 BFS 递归收集子节点批量删除。
/// </summary>
public sealed class EfCoreMenuRepository : IMenuRepository
{
    private readonly SystemAdminDbContext _db;

    public EfCoreMenuRepository(SystemAdminDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    /// <inheritdoc />
    public Task<Menu?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Menus.FirstOrDefaultAsync(m => m.Id == id, ct);

    /// <inheritdoc />
    public Task<List<Menu>> GetAllAsync(CancellationToken ct = default)
        => _db.Menus.AsNoTracking().OrderBy(m => m.Sort).ToListAsync(ct);

    /// <inheritdoc />
    public Task<List<Menu>> GetChildrenAsync(Guid parentId, CancellationToken ct = default)
        => _db.Menus.AsNoTracking()
            .Where(m => m.ParentId == parentId)
            .OrderBy(m => m.Sort)
            .ToListAsync(ct);

    /// <inheritdoc />
    public Task<Menu?> GetByPathAsync(string path, CancellationToken ct = default)
        => _db.Menus.AsNoTracking().FirstOrDefaultAsync(m => m.Path == path, ct);

    /// <inheritdoc />
    public async Task AddAsync(Menu menu, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(menu);
        await _db.Menus.AddAsync(menu, ct);
        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Menu menu, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(menu);
        _db.Menus.Update(menu);
        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var toDelete = await CollectSubtreeAsync(id, ct);
        if (toDelete.Count > 0)
        {
            _db.Menus.RemoveRange(toDelete);
            await _db.SaveChangesAsync(ct);
        }
    }

    /// <inheritdoc />
    public Task<int> CountChildrenAsync(Guid parentId, CancellationToken ct = default)
        => _db.Menus.CountAsync(m => m.ParentId == parentId, ct);

    /// <inheritdoc />
    public async Task<List<Menu>> GetByRoleAsync(string role, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        // 菜单数量 ≤ 100，全量载入后应用层精确匹配（避免 SQL LIKE 子串误匹配 "Admin" → "SuperAdmin"）
        var all = await _db.Menus.AsNoTracking().OrderBy(m => m.Sort).ToListAsync(ct);
        return all.Where(m => m.Roles.Contains(role)).ToList();
    }

    /// <summary>
    /// BFS 递归收集 rootId 及其全部子孙节点。
    /// 用于 DeleteAsync 一次性批量删除子树。
    /// </summary>
    private async Task<List<Menu>> CollectSubtreeAsync(Guid rootId, CancellationToken ct)
    {
        var result = new List<Menu>();
        var queue = new Queue<Guid>();
        queue.Enqueue(rootId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var children = await _db.Menus.Where(m => m.ParentId == current).ToListAsync(ct);
            foreach (var child in children)
            {
                result.Add(child);
                queue.Enqueue(child.Id);
            }
        }

        var root = await _db.Menus.FirstOrDefaultAsync(m => m.Id == rootId, ct);
        if (root is not null)
        {
            result.Add(root);
        }

        return result;
    }
}
