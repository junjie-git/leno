using System.IdentityModel.Tokens.Jwt;

namespace Leno.Infrastructure.Security;

/// <summary>
/// JWT 签名服务抽象（3.10 安全技术栈升级 / HS256 → RS256 过渡）。
/// <para>
/// 支持三种签名模式（由 <see cref="JwtSigningOptions.SigningMode"/> 控制）：
/// <list type="bullet">
/// <item><b>Hs256</b>：对称签名（向后兼容）。</item>
/// <item><b>Rs256</b>：非对称签名（RS256，通过 KMS 托管 RSA 私钥）。</item>
/// <item><b>Dual</b>：过渡模式，新令牌使用 RS256 签名，验签同时接受 HS256 与 RS256。</item>
/// </list>
/// </para>
/// </summary>
public interface IJwtSigningService
{
    /// <summary>
    /// 对 JWT payload 进行签名，返回紧凑序列化的 JWS 字符串。
    /// </summary>
    /// <param name="payload">JWT payload（含 iss/aud/sub/exp/nbf/jti 等 claim）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>签名的 JWT 字符串。</returns>
    Task<string> SignAsync(JwtPayload payload, CancellationToken ct);

    /// <summary>
    /// 验证 JWT 签名与生命周期。
    /// </summary>
    /// <param name="token">JWT 字符串。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>验证通过返回 true。</returns>
    Task<bool> VerifyAsync(string token, CancellationToken ct);
}
