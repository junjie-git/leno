using Leno.Identity.Domain.Aggregates;

namespace Leno.Identity.Application.Services;

/// <summary>
/// bcrypt → Argon2id 懒迁移器抽象（3.10 安全技术栈升级）。
/// 定义在 Application 层供 <see cref="AuthenticationAppService"/> 依赖，
/// 实现位于 Infrastructure 层（<c>Leno.Identity.Infrastructure.Security.BcryptToArgon2Migrator</c>）。
/// </summary>
public interface IBcryptToArgon2Migrator
{
    /// <summary>
    /// 若用户密码哈希为 bcrypt 格式，则用 Argon2id 重新哈希并持久化。
    /// </summary>
    /// <param name="user">已通过密码校验的用户聚合根。</param>
    /// <param name="plainPassword">用户明文密码（已验证正确，用于重新哈希）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>已完成迁移或无需迁移返回 true。</returns>
    Task<bool> TryMigrateAsync(User user, string plainPassword, CancellationToken ct);
}
