using Leno.Identity.Domain.Repositories;
using Leno.Identity.Domain.Services;
using Microsoft.EntityFrameworkCore;

namespace Leno.Identity.Infrastructure.Services;

/// <summary>
/// 用户唯一性校验领域服务实现（Identity BC，3.6 AuthN/AuthZ 拆分）。
/// 通过 <see cref="IUserRepository"/> 查询数据库，校验用户名/邮箱/手机号全局唯一。
/// 支持排除自身标识（更新场景传当前用户 ID，注册场景传 null）。
/// </summary>
public sealed class UserUniquenessChecker : IUserUniquenessChecker
{
    private readonly IUserRepository _userRepository;

    public UserUniquenessChecker(IUserRepository userRepository)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
    }

    /// <inheritdoc />
    public async Task<bool> IsUsernameUniqueAsync(string username, Guid? excludeUserId = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            // 空用户名视为"唯一"由聚合层校验拦截，这里不重复抛异常
            return true;
        }

        var existing = await _userRepository.GetByUsernameAsync(username, ct).ConfigureAwait(false);
        return existing is null || (excludeUserId.HasValue && existing.Id == excludeUserId.Value);
    }

    /// <inheritdoc />
    public async Task<bool> IsEmailUniqueAsync(string email, Guid? excludeUserId = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return true;
        }

        var existing = await _userRepository.GetByEmailAsync(email, ct).ConfigureAwait(false);
        return existing is null || (excludeUserId.HasValue && existing.Id == excludeUserId.Value);
    }

    /// <inheritdoc />
    public async Task<bool> IsPhoneUniqueAsync(string phone, Guid? excludeUserId = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return true;
        }

        var existing = await _userRepository.GetByPhoneAsync(phone, ct).ConfigureAwait(false);
        return existing is null || (excludeUserId.HasValue && existing.Id == excludeUserId.Value);
    }
}
