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
/// <para>
/// 下游基址由 <see cref="IBffServiceResolver"/> 按清单服务名解析；寻址失败或全部节点失败时
/// 返回与网关一致的 503 降级信封。
/// </para>
/// </summary>
[ApiController]
[Route("api/bff/orders/{orderId:guid}")]
public sealed class OrderDetailBffController : ControllerBase
{
    private const string OrderDetailSource = "order-detail";
    private const string OrderLogisticsSource = "order-logistics";

    /// <summary>清单服务名：order（Consul 注册名 leno-order-api）。</summary>
    private const string OrderServiceName = "order";

    private readonly IBffForwarderService _forwarder;
    private readonly IBffServiceResolver _serviceResolver;
    private readonly BffDagNodeFactory _nodeFactory;
    private readonly ILogger<OrderDetailBffController> _logger;

    public OrderDetailBffController(
        IBffForwarderService forwarder,
        IBffServiceResolver serviceResolver,
        BffDagNodeFactory nodeFactory,
        ILogger<OrderDetailBffController> logger)
    {
        _forwarder = forwarder ?? throw new ArgumentNullException(nameof(forwarder));
        _serviceResolver = serviceResolver ?? throw new ArgumentNullException(nameof(serviceResolver));
        _nodeFactory = nodeFactory ?? throw new ArgumentNullException(nameof(nodeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 聚合查询订单详情与物流轨迹（DAG 编排引擎）。
    /// </summary>
    /// <param name="orderId">订单 ID（GUID）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>
    /// 200 OK + <see cref="BffResponse{T}"/>；部分下游失败时 Partial=true，Errors 含失败明细；
    /// 寻址失败或全部节点失败时返回 503 降级信封。
    /// </returns>
    [HttpGet]
    public async Task<ActionResult<BffResponse<OrderDetailBffResponse>>> Get(
        [FromRoute] Guid orderId,
        CancellationToken ct)
    {
        var requestId = HttpContext.TraceIdentifier;

        string orderBase;
        try
        {
            orderBase = await _serviceResolver
                .ResolveBaseAddressAsync(OrderServiceName, ct).ConfigureAwait(false);
        }
        catch (BffServiceUnavailableException ex)
        {
            _logger.LogError(
                ex,
                "BFF OrderDetail 下游寻址失败：service={ServiceName}, consul={ConsulServiceName}, requestId={RequestId}",
                ex.ServiceName, ex.ConsulServiceName, requestId);
            return BffErrorEnvelope.DownstreamUnavailable();
        }

        // 4.3：声明式 DAG 构建——两个无依赖节点在第一波并行执行
        var graph = new AggregateBuilder()
            .AddNode(_nodeFactory.CreateNode(
                OrderDetailSource,
                new BffDownstreamRequest
                {
                    Source = OrderDetailSource,
                    ServiceUrl = $"{orderBase}/api/orders/{orderId:D}"
                },
                requestId))
            .AddNode(_nodeFactory.CreateNode(
                OrderLogisticsSource,
                new BffDownstreamRequest
                {
                    Source = OrderLogisticsSource,
                    ServiceUrl = $"{orderBase}/api/orders/{orderId:D}/logistics"
                },
                requestId))
            .Build();

        _logger.LogInformation(
            "BFF OrderDetail (DAG): executing {Count} nodes for orderId={OrderId}, requestId={RequestId}",
            graph.Count, orderId, requestId);

        var result = await _forwarder.ExecuteDagAsync(graph, ct);

        // 全部节点失败时返回网关统一 503 信封，不返回 200 + 全 null 的空对象
        if (!result.Success && !result.Partial)
        {
            _logger.LogError(
                "BFF OrderDetail 全部下游节点失败：orderId={OrderId}, requestId={RequestId}, errors={Errors}",
                orderId, requestId, string.Join("; ", result.Errors.Select(e => $"{e.Source}:{e.Message}")));
            return BffErrorEnvelope.DownstreamUnavailable();
        }

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
