using System.Text.Json;

namespace Leno.ApiGateway.Bff.Models;

/// <summary>
/// 订单详情聚合响应：订单主体 + 物流轨迹。
/// </summary>
public sealed class OrderDetailBffResponse
{
    /// <summary>订单主体（来自 Order BC <c>/api/orders/{orderId}</c>）。</summary>
    public JsonElement? Order { get; init; }

    /// <summary>物流轨迹（来自 Order BC <c>/api/orders/{orderId}/logistics</c>）。</summary>
    public JsonElement? Logistics { get; init; }
}
