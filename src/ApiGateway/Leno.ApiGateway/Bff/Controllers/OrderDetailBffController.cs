using System.Text.Json;
using Leno.ApiGateway.Bff.Dag;
using Leno.ApiGateway.Bff.Models;
using Microsoft.AspNetCore.Mvc;

namespace Leno.ApiGateway.Bff.Controllers;

/// <summary>
/// 订单详情 BFF 聚合端点。
/// <para>
/// 4.3 迁移示例：从 <see cref="IBffForwarderService.ForwardAsync{T}"/>（Parallel.ForEachAsync 无依赖并行）
/// 迁移到 <see cref="IBffForwarderService.ExecuteDagAsync"/>（DAG 编排引擎）。
/// 两个下游请求（订单详情 + 物流轨迹）相互独立，DAG 引擎将它们在第一波并行执行，
/// 等价于原 Parallel.ForEachAsync，同时获得 DAG 引擎的节点级超时与级联取消能力。
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
    private readonly BffDagNodeFactory _nodeFactory;
    private readonly ILogger<OrderDetailBffController> _logger;

    public OrderDetailBffController(
        IBffForwarderService forwarder,
        BffDagNodeFactory nodeFactory,
        ILogger<OrderDetailBffController> logger)
    {
        _forwarder = forwarder ?? throw new ArgumentNullException(nameof(forwarder));
        _nodeFactory = nodeFactory ?? throw new ArgumentNullException(nameof(nodeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 聚合查询订单详情与物流轨迹（DAG 编排引擎）。
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

        // 4.3：声明式 DAG 构建——两个无依赖节点在第一波并行执行
        var graph = new AggregateBuilder()
            .AddNode(_nodeFactory.CreateNode(
                OrderDetailSource,
                new BffDownstreamRequest
                {
                    Source = OrderDetailSource,
                    ServiceUrl = $"{OrderServiceBase}/api/orders/{orderId:D}"
                },
                requestId))
            .AddNode(_nodeFactory.CreateNode(
                OrderLogisticsSource,
                new BffDownstreamRequest
                {
                    Source = OrderLogisticsSource,
                    ServiceUrl = $"{OrderServiceBase}/api/orders/{orderId:D}/logistics"
                },
                requestId))
            .Build();

        _logger.LogInformation(
            "BFF OrderDetail (DAG): executing {Count} nodes for orderId={OrderId}, requestId={RequestId}",
            graph.Count, orderId, requestId);

        var result = await _forwarder.ExecuteDagAsync(graph, ct);

        // 从 DAG 结果中提取节点结果，组装 BffResponse
        return Ok(new BffResponse<OrderDetailBffResponse>
        {
            Success = result.Success,
            Partial = result.Partial,
            Data = new OrderDetailBffResponse
            {
                Order = result.GetResult<JsonElement>(OrderDetailSource),
                Logistics = result.GetResult<JsonElement>(OrderLogisticsSource)
            },
            Errors = result.Errors
        });
    }
}
