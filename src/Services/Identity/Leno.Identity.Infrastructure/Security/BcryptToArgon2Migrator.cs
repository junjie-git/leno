using Leno.Identity.Application.Services;
using Leno.Identity.Domain.Repositories;
using Leno.Infrastructure.Security;
using Microsoft.Extensions.Logging;

namespace Leno.Identity.Infrastructure.Security;

/// <summary>
/// bcrypt → Argon2id 懒迁移器（3.10 安全技术栈升级）。
/// <para>
/// 在用户登录成功后调用：<see cref="TryMigrateAsync"/> 检测已存哈希算法，
/// 若为 bcrypt 则使用 Argon2id 重新哈希并持久化，实现无感知懒迁移。
/// Argon2id 哈希或纯 OAuth 用户（无密码哈希）直接返回 true，不执行迁移。
/// </para>
/// </summary>
public class BcryptToArgon2Migrator : IBcryptToArgon2Migrator
{
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<BcryptToArgon2Migrator> _logger;

    public BcryptToArgon2Migrator(
        IPasswordHasher passwordHasher,
        IUserRepository userRepository,
        ILogger<BcryptToArgon2Migrator> logger)
    {
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 若用户密码哈希为 bcrypt 格式，则用 Argon2id 重新哈希并持久化。
    /// </summary>
    /// <param name="user">已通过密码校验的用户聚合根。</param>
    /// <param name="plainPassword">用户明文密码（已验证正确，用于重新哈希）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>已完成迁移或无需迁移返回 true。</returns>
    public virtual async Task<bool> TryMigrateAsync(
        Leno.Identity.Domain.Aggregates.User user,
        string plainPassword,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(user);

        // 纯 OAuth 用户无密码哈希，跳过迁移
        if (string.IsNullOrEmpty(user.PasswordHash))
        {
            return true;
        }

        PasswordHashAlgorithm algorithm;
        try
        {
            algorithm = _passwordHasher.DetectAlgorithm(user.PasswordHash);
        }
        catch (FormatException ex)
        {
            _logger.LogWarning(ex, "密码哈希格式无法识别，跳过迁移，UserId={UserId}", user.Id);
            return false;
        }

        if (algorithm != PasswordHashAlgorithm.Bcrypt)
        {
            // 已是 Argon2id，无需迁移
            return true;
        }

        // bcrypt → Argon2id 重新哈希
        var newHash = _passwordHasher.HashPassword(plainPassword);
        user.UpdatePasswordHash(newHash);

        await _userRepository.UpdateAsync(user, ct).ConfigureAwait(false);

        _logger.LogInformation(
            "密码哈希已从 bcrypt 懒迁移至 Argon2id，UserId={UserId}", user.Id);

        return true;
    }
}
