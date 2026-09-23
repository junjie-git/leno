namespace Leno.Identity.Application;

/// <summary>
/// Identity BC 的 JWT 配置，对应 appsettings.json 中 <c>Identity:Jwt</c> 节。
/// 与 <c>Leno.Infrastructure.Auth.JwtOptions</c>（用于 JwtBearer 鉴权管线，读取 <c>Jwt</c> 节）
/// 区分以避免与共享内核的鉴权配置节冲突，由 <see cref="Services.JwtTokenService"/> 用于签发访问令牌。
/// 阶段三 Wave 2（3.6 AuthN/AuthZ BC 拆分）。
/// </summary>
public sealed class JwtOptions
{
    /// <summary>JWT 发行方标识。</summary>
    public string Issuer { get; set; } = "leno-identity";

    /// <summary>JWT 受众标识。</summary>
    public string Audience { get; set; } = "leno-clients";

    /// <summary>访问令牌有效期（分钟）。</summary>
    public int AccessTokenExpirationMinutes { get; set; } = 30;

    /// <summary>刷新令牌有效期（天）。</summary>
    public int RefreshTokenExpirationDays { get; set; } = 7;

    /// <summary>配置节名称，供 <c>services.Configure&lt;JwtOptions&gt;(configuration.GetSection(SectionName))</c> 使用。</summary>
    public const string SectionName = "Identity:Jwt";
}
