namespace Leno.Identity.Application.DTOs;

/// <summary>
/// 登录请求 DTO（Identity BC，3.6 AuthN/AuthZ 拆分）。
/// <see cref="UsernameOrEmail"/> 同时承载用户名或邮箱，由应用层依据是否包含 <c>@</c> 符号路由查询。
/// </summary>
public sealed class LoginDto
{
    /// <summary>用户名或邮箱（不区分大小写匹配）。</summary>
    public string UsernameOrEmail { get; set; } = string.Empty;

    /// <summary>明文密码，应用层经 <c>IPasswordHasher</c> 校验，不落库不落日志。</summary>
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// 登录成功签发的令牌响应 DTO。
/// <see cref="AccessToken"/> 为 JWT，<see cref="RefreshToken"/> 为不透明令牌，<see cref="ExpiresAt"/> 为 UTC 时间。
/// </summary>
public sealed class TokenDto
{
    /// <summary>JWT 访问令牌（含 sub/name/role claims）。</summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>不透明刷新令牌（Base64URL 编码 32 字节随机数）。</summary>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>访问令牌过期时间（UTC），客户端据此调度刷新。</summary>
    public DateTime ExpiresAt { get; set; }
}

/// <summary>
/// 刷新令牌请求 DTO。
/// </summary>
public sealed class RefreshTokenDto
{
    /// <summary>客户端持有的不透明刷新令牌字符串。</summary>
    public string RefreshToken { get; set; } = string.Empty;
}
