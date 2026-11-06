using System.Text.Json;
using Leno.ApiGateway.Bff.Dag;
using Leno.ApiGateway.Bff.Models;
using Microsoft.AspNetCore.Mvc;

namespace Leno.ApiGateway.Bff.Controllers;

/// <summary>
/// 订单详情聚合端点（DAG 依赖链示例）。
/// <para>
/// 4.3 声明式 DAG 构建：order → {user, items} → product-snapshot
/// <list type="bullet">
///   <item><c>order</c>：查询订单主体（无依赖，第一波执行）</item>
///   <item><c>user</c>：从 order 提取 userId 后查询用户信息（依赖 order，第二波执行）</item>
///   <item><c>items</c>：查询订单项列表（依赖 order，第二波与 user 并行执行）</item>
///   <item><c>product-snapshot</c>：从 items 提取 productId 后批量查询商品快照（依赖 items，第三波执行）</item>
/// </list>
/// DAG 引擎自动拓扑排序 + 分波并行：第二波 user 与 items 无相互依赖，并行执行。
/// </para>
/// <para>
/// 下游基址不在此处硬编码：统一由 <see cref="IBffServiceResolver"/> 按
/// <c>deploy/services.manifest.json</c> 的服务名解析（本地开发取显式 BaseUrl，compose/K8s 走 Consul 发现）。
/// 任一服务无法寻址时返回与网关一致的 503 降级信封；全部节点失败时同样返回 503，不返回空对象。
/// </para>
/// </summary>
[ApiController]
[Route("api/aggregate/order-detail")]
public sealed class OrderDetailAggregateController : ControllerBase
{
    private const string OrderNode = "order";
    private const string UserNode = "user";
    private const string ItemsNode = "items";
    private const string ProductSnapshotNode = "product-snapshot";

    /// <summary>清单服务名：order（Consul 注册名 leno-order-api）。</summary>
    private const string OrderServiceName = "order";

    /// <summary>清单服务名：usercenter（Consul 注册名 leno-user-center-api）。</summary>
    private const string UserCenterServiceName = "usercenter";

    /// <summary>清单服务名：product（Consul 注册名 leno-product-api）。</summary>
    private const string ProductServiceName = "product";

    private readonly IBffForwarderService _forwarder;
    private readonly IBffServiceResolver _serviceResolver;
    private readonly BffDagNodeFactory _nodeFactory;
    private readonly ILogger<OrderDetailAggregateController> _logger;

    public OrderDetailAggregateController(
        IBffForwarderService forwarder,
        IBffServiceResolver serviceResolver,
        BffDagNodeFactory nodeFactory,
        ILogger<OrderDetailAggregateController> logger)
    {
        _forwarder = forwarder ?? throw new ArgumentNullException(nameof(forwarder));
        _serviceResolver = serviceResolver ?? throw new ArgumentNullException(nameof(serviceResolver));
        _nodeFactory = nodeFactory ?? throw new ArgumentNullException(nameof(nodeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 聚合查询订单详情：订单 + 用户 + 订单项 + 商品快照（DAG 依赖链）。
    /// </summary>
    /// <param name="orderId">订单 ID（GUID）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>
    /// 200 OK + <see cref="BffResponse{T}"/>；部分节点失败时 Partial=true，Errors 含失败明细；
    /// 寻址失败或全部节点失败时返回 503 降级信封。
    /// </returns>
    [HttpGet]
    public async Task<ActionResult<BffResponse<OrderDetailAggregateResponse>>> Get(
        [FromQuery] Guid orderId,
        CancellationToken ct)
    {
        var requestId = HttpContext.TraceIdentifier;

        string orderBase;
        string userBase;
        string productBase;
        try
        {
            orderBase = await _serviceResolver
                .ResolveBaseAddressAsync(OrderServiceName, ct).ConfigureAwait(false);
            userBase = await _serviceResolver
                .ResolveBaseAddressAsync(UserCenterServiceName, ct).ConfigureAwait(false);
            productBase = await _serviceResolver
                .ResolveBaseAddressAsync(ProductServiceName, ct).ConfigureAwait(false);
        }
        catch (BffServiceUnavailableException ex)
        {
            _logger.LogError(
                ex,
                "BFF OrderDetailAggregate 下游寻址失败：service={ServiceName}, consul={ConsulServiceName}, requestId={RequestId}",
                ex.ServiceName, ex.ConsulServiceName, requestId);
            return BffErrorEnvelope.DownstreamUnavailable();
        }

        // 4.3：声明式 DAG 构建——order → {user, items} → product-snapshot
        var graph = new AggregateBuilder()
            // 第一波：查询订单主体（无依赖）
            .AddNode(_nodeFactory.CreateNode(
                OrderNode,
                new BffDownstreamRequest
                {
                    Source = OrderNode,
                    ServiceUrl = $"{orderBase}/api/orders/{orderId:D}"
                },
                requestId))
            // 第二波：依赖 order，从订单中提取 userId 查询用户信息
            .AddNode(_nodeFactory.CreateNode(
                UserNode,
                ctx => BuildUserRequest(ctx, userBase),
                requestId))
            .DependsOn(UserNode, OrderNode)
            // 第二波：依赖 order，查询订单项列表
            .AddNode(_nodeFactory.CreateNode(
                ItemsNode,
                new BffDownstreamRequest
                {
                    Source = ItemsNode,
                    ServiceUrl = $"{orderBase}/api/orders/{orderId:D}/items"
                },
                requestId))
            .DependsOn(ItemsNode, OrderNode)
            // 第三波：依赖 items，从订单项提取 productId 批量查询商品快照
            .AddNode(_nodeFactory.CreateNode(
                ProductSnapshotNode,
                ctx => BuildProductSnapshotRequest(ctx, productBase, orderId),
                requestId))
            .DependsOn(ProductSnapshotNode, ItemsNode)
            .Build();

        _logger.LogInformation(
            "BFF OrderDetailAggregate (DAG): executing {Count} nodes with dependency chain order→{{user,items}}→snapshot, orderId={OrderId}, requestId={RequestId}",
            graph.Count, orderId, requestId);

        var result = await _forwarder.ExecuteDagAsync(graph, ct);

        // 全部节点失败（无任何可用数据）时按网关统一降级信封返回 503，避免 200 + 全 null 的空对象
        if (!result.Success && !result.Partial)
        {
            _logger.LogError(
                "BFF OrderDetailAggregate 全部下游节点失败：orderId={OrderId}, requestId={RequestId}, errors={Errors}",
                orderId, requestId, string.Join("; ", result.Errors.Select(e => $"{e.Source}:{e.Message}")));
            return BffErrorEnvelope.DownstreamUnavailable();
        }

        return Ok(new BffResponse<OrderDetailAggregateResponse>
        {
            Success = result.Success,
            Partial = result.Partial,
            Data = new OrderDetailAggregateResponse
            {
                Order = result.GetResult<JsonElement>(OrderNode),
                User = result.GetResult<JsonElement>(UserNode),
                Items = result.GetResult<JsonElement>(ItemsNode),
                ProductSnapshot = result.GetResult<JsonElement>(ProductSnapshotNode)
            },
            Errors = result.Errors
        });
    }

    /// <summary>
    /// 从 order 节点结果中提取 userId，构造 User BC 查询请求。
    /// 若 order 失败或未包含 userId，返回 null 跳过该节点。
    /// </summary>
    private static BffDownstreamRequest? BuildUserRequest(
        IReadOnlyDictionary<string, object?> ctx,
        string userCenterBase)
    {
        var order = ctx.GetJsonValue(OrderNode);
        if (order is null || !order.Value.TryGetProperty("userId", out var userIdElement))
        {
            return null;
        }
        var userId = userIdElement.GetGuid();
        return new BffDownstreamRequest
        {
            Source = UserNode,
            ServiceUrl = $"{userCenterBase}/api/users/{userId:D}"
        };
    }

    /// <summary>
    /// 从 items 节点结果中提取 productId 列表，构造 Product BC 批量查询请求。
    /// 若 items 失败或为空，返回 null 跳过该节点。
    /// </summary>
    private static BffDownstreamRequest? BuildProductSnapshotRequest(
        IReadOnlyDictionary<string, object?> ctx,
        string productBase,
        Guid orderId)
    {
        var items = ctx.GetJsonValue(ItemsNode);
        if (items is null)
        {
            return null;
        }

        // 从订单项数组中提取 productId，构造批量查询 URL
        if (items.Value.ValueKind == JsonValueKind.Array && items.Value.GetArrayLength() > 0)
        {
            var productIds = new List<string>();
            foreach (var item in items.Value.EnumerateArray())
            {
                if (item.TryGetProperty("productId", out var pid))
                {
                    productIds.Add(pid.GetGuid().ToString("D"));
                }
            }
            if (productIds.Count == 0)
            {
                return null;
            }
            var idsQuery = string.Join(",", productIds);
            return new BffDownstreamRequest
            {
                Source = ProductSnapshotNode,
                ServiceUrl = $"{productBase}/api/products/snapshot?ids={idsQuery}"
            };
        }

        // items 非数组或为空：降级为基于 orderId 查询商品快照
        return new BffDownstreamRequest
        {
            Source = ProductSnapshotNode,
            ServiceUrl = $"{productBase}/api/products/snapshot?orderId={orderId:D}"
        };
    }
}
