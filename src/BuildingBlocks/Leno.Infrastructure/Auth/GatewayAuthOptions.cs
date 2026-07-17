using Microsoft.AspNetCore.Authentication;

namespace Leno.Infrastructure.Auth;

/// <summary>
/// 网关头认证选项。
/// </summary>
public sealed class GatewayAuthOptions : AuthenticationSchemeOptions
{
    /// <summary>头前缀，默认 "X-"。</summary>
    public string HeaderPrefix { get; set; } = "X-";

    /// <summary>是否校验 X-Internal-Call 头确认请求来自网关。</summary>
    public bool RequireInternalCallHeader { get; set; } = false;
}
