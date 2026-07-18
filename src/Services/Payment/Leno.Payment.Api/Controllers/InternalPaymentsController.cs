using Leno.Payment.Application;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Mvc;

namespace Leno.Payment.Api.Controllers;

/// <summary>
/// 支付域内部查询控制器，供其他微服务调用。
/// 受 InternalApiKeyMiddleware 保护。
/// </summary>
[ApiController]
public sealed class InternalPaymentsController : ControllerBase
{
    private readonly IPaymentInternalQueryService _queryService;

    public InternalPaymentsController(IPaymentInternalQueryService queryService)
    {
        ArgumentNullException.ThrowIfNull(queryService);
        _queryService = queryService;
    }

    /// <summary>按订单标识查询支付单概要信息（内部接口）。</summary>
    [HttpGet("internal/v1/payments/{orderId:guid}/info")]
    [Obsolete("双路由期保留，1 周后下线，请使用 internal/v1/... 路由")]
    [HttpGet("internal/payments/{orderId:guid}/info")]
    [ProducesResponseType(typeof(ApiResponse<PaymentInfoResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPaymentInfoAsync(Guid orderId, CancellationToken ct)
    {
        var result = await _queryService.GetPaymentInfoByOrderIdAsync(orderId, ct);
        if (result is null)
        {
            return NotFound(ApiResponse.Fail(StatusCodes.Status404NotFound, "支付单不存在"));
        }

        return Ok(ApiResponse.Success(result));
    }
}
