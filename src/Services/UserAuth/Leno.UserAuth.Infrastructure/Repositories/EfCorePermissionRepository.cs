using Leno.UserAuth.Domain.Aggregates;
using Leno.UserAuth.Domain.Repositories;
using Leno.UserAuth.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Leno.UserAuth.Infrastructure.Repositories;

/// <summary>
/// 角色权限仓储 EF Core 实现。
/// </summary>
public sealed class EfCorePermissionRepository : IPermissionRepository
{
    private readonly UserAuthDbContext _context;

    public EfCorePermissionRepository(UserAuthDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public Task<Role?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _context.Roles.FirstOrDefaultAsync(r => r.Id == id, ct);

    /// <inheritdoc />
    public Task<Role?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        var normalized = name?.Trim() ?? string.Empty;
        return _context.Roles.FirstOrDefaultAsync(r => r.Name == normalized, ct);
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<Role> Items, int Total)> QueryAsync(
        string? keyword = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = _context.Roles.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            query = query.Where(r => EF.Functions.Like(r.Name, $"%{kw}%")
                || (r.Description != null && EF.Functions.Like(r.Description, $"%{kw}%")));
        }

        var total = await query.CountAsync(ct);
        var safePage = page < 1 ? 1 : page;
        var safePageSize = pageSize is <= 0 or > 100 ? 20 : pageSize;

        var items = await query
            .OrderBy(r => r.Name)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Role>> GetRolesByPermissionAsync(string resourceKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(resourceKey))
        {
            return Array.Empty<Role>();
        }

        // 权限以 JSON nvarchar(max) 存储（格式 [{"resourceKey":"...","description":"..."},...]）。
        // 使用 SQL Server OPENJSON 在数据库端解析 JSON 权限列并按 resourceKey 精确过滤，
        // 避免全表加载后内存反序列化过滤（原实现 P1-4 问题）。
        // OPENJSON 对 NULL/空数组返回 0 行，无需额外判空。
        var roles = await _context.Roles
            .FromSqlInterpolated($@"
                SELECT r.* FROM roles r
                CROSS APPLY OPENJSON(r.permissions) WITH (resourceKey nvarchar(256) '$.resourceKey') AS p
                WHERE p.resourceKey = {resourceKey}")
            .AsNoTracking()
            .ToListAsync(ct);

        return roles;
    }

    /// <inheritdoc />
    public async Task<bool> HasUserReferencesAsync(Guid roleId, CancellationToken ct = default)
    {
        var role = await _context.Roles.FirstOrDefaultAsync(r => r.Id == roleId, ct);
        if (role is null)
        {
            return false;
        }

        // 如果角色名称匹配 RoleType 枚举，检查是否有用户拥有该角色类型
        if (Enum.TryParse<RoleType>(role.Name, ignoreCase: true, out var roleType))
        {
            return await _context.Users.AnyAsync(u => u.Roles.Any(r => r.Value == roleType), ct);
        }

        // 自定义角色暂无直接用户引用
        return false;
    }

    /// <inheritdoc />
    public Task AddAsync(Role role, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(role);
        _context.Roles.Add(role);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UpdateAsync(Role role, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(role);
        if (_context.Entry(role).State == EntityState.Detached)
        {
            _context.Roles.Attach(role);
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(Role role, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(role);
        _context.Roles.Remove(role);
        return Task.CompletedTask;
    }
}