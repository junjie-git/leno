using Leno.Order.Application;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Mvc;

namespace Leno.Order.Api.Controllers;

/// <summary>
/// 订单域内部查询控制器，供其他微服务调用。
/// 受 InternalApiKeyMiddleware 保护。
/// </summary>
[ApiController]
public sealed class InternalOrdersController : ControllerBase
{
    private readonly IOrderInternalQueryService _queryService;

    public InternalOrdersController(IOrderInternalQueryService queryService)
    {
        ArgumentNullException.ThrowIfNull(queryService);
        _queryService = queryService;
    }

    [HttpGet("internal/v1/orders/{orderId:guid}/status")]
    [Obsolete("双路由期保留，将于 2026-08-15 下线；旧路由 internal/orders/{orderId}/status 调用方请切换到 internal/v1/orders/{orderId}/status（统一 v1 前缀），跟踪 issue: order-bc/internal-route-deprecation-2026-08")]
    [HttpGet("internal/orders/{orderId:guid}/status")]
    [ProducesResponseType(typeof(ApiResponse<OrderStatusResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrderStatusAsync(Guid orderId, CancellationToken ct)
    {
        var result = await _queryService.GetOrderStatusAsync(orderId, ct);
        if (result is null)
        {
            return NotFound(ApiResponse.Fail(StatusCodes.Status404NotFound, "订单不存在"));
        }
        return Ok(ApiResponse.Success(result));
    }
}
