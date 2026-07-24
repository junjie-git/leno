using System.Text.Json;

namespace Leno.ApiGateway.Bff.Models;

/// <summary>
/// 订单详情聚合响应（DAG 依赖链示例）：
/// 订单主体 + 用户信息 + 订单项 + 商品快照。
/// <para>
/// 依赖链：order → {user, items} → product-snapshot
/// </para>
/// </summary>
public sealed class OrderDetailAggregateResponse
{
    /// <summary>订单主体（来自 Order BC <c>/api/orders/{orderId}</c>）。</summary>
    public JsonElement? Order { get; init; }

    /// <summary>用户信息（依赖 order，从订单中提取 userId 后调用 User BC）。</summary>
    public JsonElement? User { get; init; }

    /// <summary>订单项列表（依赖 order，来自 Order BC <c>/api/orders/{orderId}/items</c>）。</summary>
    public JsonElement? Items { get; init; }

    /// <summary>商品快照（依赖 items，从订单项提取 productId 后调用 Product BC 批量查询）。</summary>
    public JsonElement? ProductSnapshot { get; init; }
}
