using System.Linq.Expressions;
using Leno.UserAuth.Domain.Aggregates;
using Leno.UserAuth.Domain.Services;
using Microsoft.EntityFrameworkCore;

namespace Leno.UserAuth.Infrastructure.Services;

/// <summary>
/// 用户唯一性校验实现，查询数据库校验用户名/邮箱/手机号全局唯一。
/// 支持排除自身标识（更新场景）。邮箱与存储形式一致转小写比较。
/// </summary>
public sealed class UserUniquenessChecker : IUserUniquenessChecker
{
    private readonly UserAuthDbContext _context;

    public UserUniquenessChecker(UserAuthDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public Task<bool> IsUsernameUniqueAsync(string username, Guid? excludeUserId = null, CancellationToken ct = default)
    {
        var normalized = username?.Trim() ?? string.Empty;
        return ExistsAsync(u => u.Username == normalized, excludeUserId, ct);
    }

    /// <inheritdoc />
    public Task<bool> IsEmailUniqueAsync(string email, Guid? excludeUserId = null, CancellationToken ct = default)
    {
        var normalized = string.IsNullOrWhiteSpace(email) ? string.Empty : email.Trim().ToLowerInvariant();
        return ExistsAsync(u => u.Email == normalized, excludeUserId, ct);
    }

    /// <inheritdoc />
    public Task<bool> IsPhoneUniqueAsync(string phone, Guid? excludeUserId = null, CancellationToken ct = default)
    {
        var normalized = phone?.Trim() ?? string.Empty;
        return ExistsAsync(u => u.PhoneNumber == normalized, excludeUserId, ct);
    }

    private async Task<bool> ExistsAsync(
        Expression<Func<User, bool>> predicate,
        Guid? excludeUserId,
        CancellationToken ct)
    {
        var query = _context.Users.AsNoTracking();
        if (excludeUserId.HasValue)
        {
            var exclude = excludeUserId.Value;
            query = query.Where(u => u.Id != exclude);
        }

        var exists = await query.AnyAsync(predicate, ct);
        return !exists;
    }
}
