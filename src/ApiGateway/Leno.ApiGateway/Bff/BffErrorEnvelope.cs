using Microsoft.AspNetCore.Mvc;

namespace Leno.ApiGateway.Bff;

/// <summary>
/// BFF 降级响应信封：与网关 <c>FallbackResponseMiddleware</c> 的格式保持一致
/// （<c>{ "code": 503, "message": "服务暂时不可用，请稍后重试", "data": null }</c>）。
/// <para>
/// 仅在"全部下游都不可用"时使用；部分下游成功时仍返回 200 与 <see cref="Models.BffResponse{T}"/>，
/// 通过 <c>Errors</c> 暴露失败明细。
/// </para>
/// </summary>
internal static class BffErrorEnvelope
{
    /// <summary>降级响应中的业务码（与 HTTP 状态码一致）。</summary>
    internal const int DownstreamUnavailableCode = 503;

    /// <summary>降级响应文案（与网关熔断降级文案一致）。</summary>
    internal const string DownstreamUnavailableMessage = "服务暂时不可用，请稍后重试";

    /// <summary>
    /// 构造 503 降级响应。
    /// </summary>
    /// <returns>HTTP 503 + 网关统一错误信封。</returns>
    internal static ObjectResult DownstreamUnavailable()
    {
        return new ObjectResult(new
        {
            code = DownstreamUnavailableCode,
            message = DownstreamUnavailableMessage,
            data = (object?)null
        })
        {
            StatusCode = StatusCodes.Status503ServiceUnavailable
        };
    }
}
