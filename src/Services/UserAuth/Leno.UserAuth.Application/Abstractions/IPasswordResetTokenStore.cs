namespace Leno.UserAuth.Application.Abstractions;

/// <summary>
/// 密码重置令牌存储抽象。
/// 忘记密码流程中签发一次性重置令牌（10 分钟），重置密码时消费令牌换取 userId。
/// 应用层只依赖此抽象，不感知底层存储实现。
/// </summary>
public interface IPasswordResetTokenStore
{
    /// <summary>
    /// 为指定用户签发密码重置令牌，TTL 由调用方指定（通常 10 分钟）。
    /// </summary>
    /// <returns>不透明的重置令牌字符串。</returns>
    Task<string> IssueAsync(Guid userId, TimeSpan ttl, CancellationToken ct = default);

    /// <summary>
    /// 校验并消费重置令牌：成功返回 userId 并立即删除以防止重放，失败返回 null。
    /// </summary>
    Task<Guid?> ValidateAndConsumeAsync(string token, CancellationToken ct = default);
}
