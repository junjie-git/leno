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

    private const string OrderServiceBase = "http://order-api:8080";
    private const string UserServiceBase = "http://user-api:8080";
    private const string ProductServiceBase = "http://product-api:8080";

    private readonly IBffForwarderService _forwarder;
    private readonly BffDagNodeFactory _nodeFactory;
    private readonly ILogger<OrderDetailAggregateController> _logger;

    public OrderDetailAggregateController(
        IBffForwarderService forwarder,
        BffDagNodeFactory nodeFactory,
        ILogger<OrderDetailAggregateController> logger)
    {
        _forwarder = forwarder ?? throw new ArgumentNullException(nameof(forwarder));
        _nodeFactory = nodeFactory ?? throw new ArgumentNullException(nameof(nodeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 聚合查询订单详情：订单 + 用户 + 订单项 + 商品快照（DAG 依赖链）。
    /// </summary>
    /// <param name="orderId">订单 ID（GUID）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>
    /// 200 OK + <see cref="BffResponse{T}"/>；部分节点失败时 Partial=true，Errors 含失败明细。
    /// </returns>
    [HttpGet]
    public async Task<ActionResult<BffResponse<OrderDetailAggregateResponse>>> Get(
        [FromQuery] Guid orderId,
        CancellationToken ct)
    {
        var requestId = HttpContext.TraceIdentifier;

        // 4.3：声明式 DAG 构建——order → {user, items} → product-snapshot
        var graph = new AggregateBuilder()
            // 第一波：查询订单主体（无依赖）
            .AddNode(_nodeFactory.CreateNode(
                OrderNode,
                new BffDownstreamRequest
                {
                    Source = OrderNode,
                    ServiceUrl = $"{OrderServiceBase}/api/orders/{orderId:D}"
                },
                requestId))
            // 第二波：依赖 order，从订单中提取 userId 查询用户信息
            .AddNode(_nodeFactory.CreateNode(
                UserNode,
                ctx => BuildUserRequest(ctx),
                requestId))
            .DependsOn(UserNode, OrderNode)
            // 第二波：依赖 order，查询订单项列表
            .AddNode(_nodeFactory.CreateNode(
                ItemsNode,
                new BffDownstreamRequest
                {
                    Source = ItemsNode,
                    ServiceUrl = $"{OrderServiceBase}/api/orders/{orderId:D}/items"
                },
                requestId))
            .DependsOn(ItemsNode, OrderNode)
            // 第三波：依赖 items，从订单项提取 productId 批量查询商品快照
            .AddNode(_nodeFactory.CreateNode(
                ProductSnapshotNode,
                ctx => BuildProductSnapshotRequest(ctx, orderId),
                requestId))
            .DependsOn(ProductSnapshotNode, ItemsNode)
            .Build();

        _logger.LogInformation(
            "BFF OrderDetailAggregate (DAG): executing {Count} nodes with dependency chain order→{{user,items}}→snapshot, orderId={OrderId}, requestId={RequestId}",
            graph.Count, orderId, requestId);

        var result = await _forwarder.ExecuteDagAsync(graph, ct);

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
    private static BffDownstreamRequest? BuildUserRequest(IReadOnlyDictionary<string, object?> ctx)
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
            ServiceUrl = $"{UserServiceBase}/api/users/{userId:D}"
        };
    }

    /// <summary>
    /// 从 items 节点结果中提取 productId 列表，构造 Product BC 批量查询请求。
    /// 若 items 失败或为空，返回 null 跳过该节点。
    /// </summary>
    private static BffDownstreamRequest? BuildProductSnapshotRequest(
        IReadOnlyDictionary<string, object?> ctx,
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
                ServiceUrl = $"{ProductServiceBase}/api/products/snapshot?ids={idsQuery}"
            };
        }

        // items 非数组或为空：降级为基于 orderId 查询商品快照
        return new BffDownstreamRequest
        {
            Source = ProductSnapshotNode,
            ServiceUrl = $"{ProductServiceBase}/api/products/snapshot?orderId={orderId:D}"
        };
    }
}
