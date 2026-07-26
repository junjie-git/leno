namespace Leno.Identity.Application.Abstractions;

/// <summary>
/// OAuth2 state 临时存储抽象，用于 CSRF 防护与回调参数（provider、redirectUri）的会话关联。
/// 应用层只依赖此抽象，不感知底层 Redis / 内存缓存实现，便于替换存储介质。
/// 从 UserAuth BC 迁入 Identity BC（3.6 AuthN/AuthZ 拆分）。
/// </summary>
public interface IOAuthStateStore
{
    /// <summary>
    /// 存储 OAuth2 state 与其关联的 provider / redirectUri，TTL 由调用方指定（通常 5 分钟）。
    /// </summary>
    /// <param name="state">不透明的 state 字符串，由调用方生成并写入第三方授权 URL。</param>
    /// <param name="provider">小写化的 OAuth2 提供方标识（如 google / wechat / alipay）。</param>
    /// <param name="redirectUri">回调完成后跳转的最终业务 redirectUri。</param>
    /// <param name="ttl">state 有效期。</param>
    /// <param name="ct">取消令牌。</param>
    Task StoreAsync(string state, string provider, string redirectUri, TimeSpan ttl, CancellationToken ct = default);

    /// <summary>
    /// 校验并消费 state：成功返回 <see cref="OAuthStateData"/>，并立即删除以防止重放；失败返回 null。
    /// </summary>
    /// <param name="state">待消费的 state 字符串。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>state 关联数据；无效或已过期返回 null。</returns>
    Task<OAuthStateData?> ConsumeAsync(string state, CancellationToken ct = default);
}

/// <summary>
/// OAuth2 state 关联数据，由 <see cref="IOAuthStateStore.ConsumeAsync"/> 返回。
/// </summary>
public sealed record OAuthStateData(string Provider, string RedirectUri);
