using Leno.UserAuth.Domain.Aggregates;
using Leno.UserAuth.Domain.Repositories;
using Leno.UserAuth.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Leno.UserAuth.Infrastructure.Repositories;

/// <summary>
/// 用户仓储 EF Core 实现。
/// 单实体查询带跟踪（写场景依赖变更跟踪保留 owned 集合的新增元素 Added 状态）；
/// <see cref="QueryAsync"/> 为只读分页，使用 <see cref="EntityFrameworkQueryableExtensions.AsNoTracking{TEntity}"/>。
/// </summary>
public sealed class EfCoreUserRepository : IUserRepository
{
    private readonly UserAuthDbContext _context;

    public EfCoreUserRepository(UserAuthDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _context.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    /// <inheritdoc />
    public Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default)
    {
        var normalized = username?.Trim() ?? string.Empty;
        return _context.Users.FirstOrDefaultAsync(u => u.Username == normalized, ct);
    }

    /// <inheritdoc />
    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        var normalized = string.IsNullOrWhiteSpace(email) ? string.Empty : email.Trim().ToLowerInvariant();
        return _context.Users.FirstOrDefaultAsync(u => u.Email == normalized, ct);
    }

    /// <inheritdoc />
    public Task<User?> GetByPhoneAsync(string phone, CancellationToken ct = default)
    {
        var normalized = phone?.Trim() ?? string.Empty;
        return _context.Users.FirstOrDefaultAsync(u => u.PhoneNumber == normalized, ct);
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<User> Items, int Total)> QueryAsync(
        string? keyword = null,
        string? role = null,
        string? status = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = _context.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            query = query.Where(u => EF.Functions.Like(u.Username, $"%{kw}%")
                || (u.Email != null && EF.Functions.Like(u.Email, $"%{kw}%"))
                || (u.PhoneNumber != null && EF.Functions.Like(u.PhoneNumber, $"%{kw}%")));
        }

        if (!string.IsNullOrWhiteSpace(role)
            && Enum.TryParse<RoleType>(role, ignoreCase: true, out var roleType))
        {
            query = query.Where(u => u.Roles.Any(r => r.Value == roleType));
        }

        if (!string.IsNullOrWhiteSpace(status)
            && Enum.TryParse<AccountStatus>(status, ignoreCase: true, out var statusValue))
        {
            query = query.Where(u => u.Status == statusValue);
        }

        var total = await query.CountAsync(ct);
        var safePage = page < 1 ? 1 : page;
        var safePageSize = pageSize is <= 0 or > 100 ? 20 : pageSize;

        var items = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    /// <inheritdoc />
    public Task AddAsync(User user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        _context.Users.Add(user);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UpdateAsync(User user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        // 实体已通过 Get* 查询纳入跟踪；仅当脱离跟踪时附加，避免对 owned 集合调用 Update 覆盖新增元素的 Added 状态。
        if (_context.Entry(user).State == EntityState.Detached)
        {
            _context.Users.Attach(user);
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(User user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        _context.Users.Remove(user);
        return Task.CompletedTask;
    }
}
