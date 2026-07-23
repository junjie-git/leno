using Leno.Identity.Domain.Aggregates;
using Leno.Identity.Domain.Repositories;
using Leno.Identity.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Leno.Identity.Infrastructure.Repositories;

/// <summary>
/// 用户仓储 EF Core 实现（Identity BC，3.6 AuthN/AuthZ 拆分）。
/// 从 UserAuth BC 的 EfCoreUserRepository 演化而来，移除按角色过滤的重载（角色已迁至 AccessControl BC）。
/// 查询方法默认使用 <c>AsNoTracking</c>；写操作仅 Attach/Update 聚合，由 <c>IUnitOfWork</c> 统一提交。
/// </summary>
public sealed class EfCoreUserRepository : IUserRepository
{
    private readonly IdentityDbContext _context;

    public EfCoreUserRepository(IdentityDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc />
    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _context.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    /// <inheritdoc />
    public Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return Task.FromResult<User?>(null);
        }

        // 用户名在持久化时已 ToLowerInvariant，查询时同样归一化以命中唯一索引
        var normalized = username.Trim().ToLowerInvariant();
        return _context.Users.FirstOrDefaultAsync(u => u.Username == normalized, ct);
    }

    /// <inheritdoc />
    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return Task.FromResult<User?>(null);
        }

        var normalized = email.Trim().ToLowerInvariant();
        return _context.Users.FirstOrDefaultAsync(u => u.Email == normalized, ct);
    }

    /// <inheritdoc />
    public Task<User?> GetByPhoneAsync(string phone, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return Task.FromResult<User?>(null);
        }

        var normalized = phone.Trim();
        return _context.Users.FirstOrDefaultAsync(u => u.PhoneNumber == normalized, ct);
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<User> Items, int Total)> QueryAsync(
        string? keyword = null,
        string? status = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        if (page < 1)
        {
            page = 1;
        }

        if (pageSize is < 1 or > 100)
        {
            pageSize = 20;
        }

        var query = _context.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim().ToLowerInvariant();
            query = query.Where(u =>
                u.Username.ToLower().Contains(kw) ||
                (u.Email != null && u.Email.ToLower().Contains(kw)) ||
                (u.PhoneNumber != null && u.PhoneNumber.Contains(kw)));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            // status 字符串须可解析为 AccountStatus 枚举，否则忽略过滤
            if (Enum.TryParse<AccountStatus>(status.Trim(), ignoreCase: true, out var statusValue))
            {
                query = query.Where(u => u.Status == statusValue);
            }
        }

        var total = await query.CountAsync(ct).ConfigureAwait(false);

        var items = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return (items, total);
    }

    /// <inheritdoc />
    public Task<User?> FindByExternalLoginAsync(string provider, string providerUserId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(providerUserId))
        {
            return Task.FromResult<User?>(null);
        }

        var normalizedProvider = provider.Trim().ToLowerInvariant();
        var normalizedProviderUserId = providerUserId.Trim();

        // ExternalLogin 为 User 的 owned collection，通过 EF Core 导航属性匹配
        return _context.Users
            .FirstOrDefaultAsync(u =>
                u.ExternalLogins.Any(el =>
                    el.Provider == normalizedProvider &&
                    el.ProviderUserId == normalizedProviderUserId), ct);
    }

    /// <inheritdoc />
    public Task AddAsync(User aggregate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        _context.Users.Add(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UpdateAsync(User aggregate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        if (_context.Entry(aggregate).State == EntityState.Detached)
        {
            _context.Users.Attach(aggregate);
        }
        _context.Entry(aggregate).State = EntityState.Modified;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(User aggregate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        _context.Users.Remove(aggregate);
        return Task.CompletedTask;
    }
}
