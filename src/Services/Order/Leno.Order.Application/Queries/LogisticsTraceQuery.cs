namespace Leno.Order.Application.Queries;

/// <summary>
/// 物流轨迹查询参数（CQRS 读侧 Query）。
/// 由 <see cref="LogisticsTraceQueryHandler"/> 处理，加载订单聚合后委托 <c>ILogisticsTrackingService</c> 调用第三方物流 API。
/// 双发期 2 周内与 <c>OrderAppService.GetLogisticsTraceAsync</c> 并存，2 周后 Controller 切换到本 Query。
/// </summary>
public sealed class LogisticsTraceQuery
{
    /// <summary>订单标识。</summary>
    public Guid OrderId { get; init; }
}
