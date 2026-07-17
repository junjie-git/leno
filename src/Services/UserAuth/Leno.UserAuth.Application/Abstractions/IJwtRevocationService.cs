namespace Leno.UserAuth.Application.Abstractions;

public interface IJwtRevocationService
{
    /// <summary>吊销指定 jti 的 token。</summary>
    Task RevokeAsync(string jti, TimeSpan ttl, CancellationToken ct = default);
}
