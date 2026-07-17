namespace Leno.ApiGateway.Services;

/// <summary>
/// JWT 黑名单服务，检查 token jti 是否已被吊销。
/// </summary>
public interface IJwtBlacklistService
{
    /// <summary>检查 jti 是否在黑名单中。</summary>
    Task<bool> IsRevokedAsync(string jti, CancellationToken ct = default);

    /// <summary>吊销 jti（登出时调用），TTL 为 token 剩余有效期。</summary>
    Task RevokeAsync(string jti, TimeSpan ttl, CancellationToken ct = default);
}
