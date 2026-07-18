using Leno.ApiGateway.Bff.Models;
using Microsoft.AspNetCore.Mvc;

namespace Leno.ApiGateway.Bff.Controllers;

/// <summary>
/// 订单详情 BFF 聚合端点。
/// <para>
/// 并行调用 Order BC 的订单详情接口与物流轨迹接口，聚合返回 <see cref="OrderDetailBffResponse"/>。
/// </para>
/// </summary>
[ApiController]
[Route("api/bff/orders/{orderId:guid}")]
public sealed class OrderDetailBffController : ControllerBase
{
    private const string OrderDetailSource = "order-detail";
    private const string OrderLogisticsSource = "order-logistics";

    private const string OrderServiceBase = "http://order-api:8080";

    private readonly IBffForwarderService _forwarder;
    private readonly ILogger<OrderDetailBffController> _logger;

    public OrderDetailBffController(
        IBffForwarderService forwarder,
        ILogger<OrderDetailBffController> logger)
    {
        _forwarder = forwarder ?? throw new ArgumentNullException(nameof(forwarder));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 聚合查询订单详情与物流轨迹。
    /// </summary>
    /// <param name="orderId">订单 ID（GUID）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>
    /// 200 OK + <see cref="BffResponse{T}"/>；部分下游失败时 Partial=true，Errors 含失败明细。
    /// </returns>
    [HttpGet]
    public async Task<ActionResult<BffResponse<OrderDetailBffResponse>>> Get(
        [FromRoute] Guid orderId,
        CancellationToken ct)
    {
        var requestId = HttpContext.TraceIdentifier;
        var requests = new BffDownstreamRequest[]
        {
            new()
            {
                Source = OrderDetailSource,
                ServiceUrl = $"{OrderServiceBase}/api/orders/{orderId:D}"
            },
            new()
            {
                Source = OrderLogisticsSource,
                ServiceUrl = $"{OrderServiceBase}/api/orders/{orderId:D}/logistics"
            }
        };

        _logger.LogInformation(
            "BFF OrderDetail: forwarding {Count} downstream requests for orderId={OrderId}, requestId={RequestId}",
            requests.Length, orderId, requestId);

        var response = await _forwarder.ForwardAsync(
            requestId,
            requests,
            dict => new OrderDetailBffResponse
            {
                Order = dict.GetValueOrDefault(OrderDetailSource),
                Logistics = dict.GetValueOrDefault(OrderLogisticsSource)
            },
            ct);

        return Ok(response);
    }
}
