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
            var escaped = EscapeLikePattern(keyword.Trim());
            query = query.Where(u => EF.Functions.Like(u.Username, $"%{escaped}%", "\\")
                || (u.Email != null && EF.Functions.Like(u.Email, $"%{escaped}%", "\\"))
                || (u.PhoneNumber != null && EF.Functions.Like(u.PhoneNumber, $"%{escaped}%", "\\")));
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

    /// <summary>
    /// 转义 LIKE 通配符（% / _ / \），使搜索关键字作为字面量匹配而非模式匹配。
    /// 使用反斜杠作为 ESCAPE 字符（与 <c>EF.Functions.Like</c> 第三参数一致）。
    /// </summary>
    private static string EscapeLikePattern(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        return input.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
    }

    /// <inheritdoc />
    public Task AddAsync(User user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        _context.Users.Add(user);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    /// <remarks>
    /// ⚠️ 注意（P2-9）：
    /// 本方法不调用 <c>DbContext.Update(entity)</c>，因为 Update 会将实体及所有 owned 集合
    /// 标记为 <c>Modified</c>，覆盖 owned 集合新增元素的 <c>Added</c> 状态，导致插入变为更新。
    /// <para>
    /// 正常使用模式：应用层通过 <c>GetByIdAsync</c> 等查询方法加载实体（已纳入变更跟踪），
    /// 调用聚合行为方法修改字段后调用本方法（no-op，仅确保已跟踪），最后由
    /// <c>IUnitOfWork.SaveEntitiesAsync</c> 统一持久化。
    /// </para>
    /// <para>
    /// ⚠️ 若实体从外部传入且处于 <c>Detached</c> 状态，<c>Attach</c> 后实体状态为 <c>Unchanged</c>，
    /// 对导航集合与字段的修改不会被变更跟踪检测。调用方须显式标记修改字段：
    /// <c>_context.Entry(user).Property(x => x.SomeField).IsModified = true;</c>
    /// 推荐始终通过仓储查询加载实体后再修改，避免 Detached 场景。
    /// </para>
    /// </remarks>
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

    /// <inheritdoc />
    public Task<User?> FindByExternalLoginAsync(string provider, string providerUserId, CancellationToken ct = default)
    {
        var normalizedProvider = (provider ?? string.Empty).Trim().ToLowerInvariant();
        var normalizedProviderUserId = (providerUserId ?? string.Empty).Trim();

        return _context.Users
            .FirstOrDefaultAsync(u => u.ExternalLogins.Any(el =>
                el.Provider == normalizedProvider && el.ProviderUserId == normalizedProviderUserId), ct);
    }
}
