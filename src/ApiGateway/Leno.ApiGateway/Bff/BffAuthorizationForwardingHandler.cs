using Microsoft.AspNetCore.Http;

namespace Leno.ApiGateway.Bff;

/// <summary>
/// 把调用方的 <c>Authorization</c> 头透传给下游 BC。
/// <para>
/// 网关在进入 BFF 控制器前已完成 JWT 校验，但下游 BC 的 <c>[Authorize]</c> 端点仍要求同一 Bearer token；
/// 该处理器只复制调用方自己的凭据，不修改鉴权模型、不扩大信任边界。
/// 由于 <see cref="BffForwarderService"/> 与 <see cref="Dag.BffDagNodeFactory"/> 共用同一个命名 HttpClient，
/// 该处理器同时覆盖并行聚合与 DAG 编排两条调用链。
/// </para>
/// </summary>
public sealed class BffAuthorizationForwardingHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>构造处理器。</summary>
    /// <param name="httpContextAccessor">当前请求上下文访问器。</param>
    public BffAuthorizationForwardingHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Headers.Authorization is null)
        {
            var incoming = _httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
            if (!string.IsNullOrWhiteSpace(incoming))
            {
                try
                {
                    request.Headers.Authorization = System.Net.Http.Headers.AuthenticationHeaderValue.Parse(incoming);
                }
                catch (FormatException)
                {
                    // 非法 Authorization 头不阻断下游调用：下游会按未认证返回 401，
                    // 再由 BFF 统一转为 503 降级或 Partial 错误明细。
                }
            }
        }

        return base.SendAsync(request, cancellationToken);
    }
}
