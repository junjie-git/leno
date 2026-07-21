namespace Leno.UserAuth.Application.Abstractions;

/// <summary>
/// 双因子认证临时令牌存储抽象。
/// 登录流程中检测到用户启用 2FA 时签发短期临时令牌（5 分钟），
/// 二次验证通过后消费令牌换取完整 AccessToken。
/// 应用层只依赖此抽象，不感知底层存储实现。
/// </summary>
public interface ITwoFactorTempTokenStore
{
    /// <summary>
    /// 为指定用户签发 2FA 临时令牌，TTL 由调用方指定（通常 5 分钟）。
    /// </summary>
    /// <returns>不透明的临时令牌字符串。</returns>
    Task<string> IssueAsync(Guid userId, TimeSpan ttl, CancellationToken ct = default);

    /// <summary>
    /// 校验并消费临时令牌：成功返回 userId 并立即删除以防止重放，失败返回 null。
    /// </summary>
    Task<Guid?> ValidateAndConsumeAsync(string tempToken, CancellationToken ct = default);
}
