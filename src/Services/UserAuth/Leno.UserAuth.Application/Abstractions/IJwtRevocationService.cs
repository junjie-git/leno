namespace Leno.UserAuth.Application.Abstractions;

public interface IJwtRevocationService
{
    /// <summary>吊销指定 jti 的 token（登出路径）。</summary>
    Task RevokeAsync(string jti, TimeSpan ttl, CancellationToken ct = default);

    /// <summary>
    /// 按用户标识批量吊销该用户所有已签发的 JWT（角色变更 / 禁用 / 锁定路径）。
    /// 把 userId 写入短期黑名单（TTL = JWT 最大有效期），网关在验证 JWT 时同时校验 userId 黑名单。
    /// </summary>
    Task RevokeUserAsync(Guid userId, CancellationToken ct = default);
}
