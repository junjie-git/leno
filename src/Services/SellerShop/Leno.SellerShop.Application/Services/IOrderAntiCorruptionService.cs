namespace Leno.SellerShop.Application.Services;

/// <summary>
/// 订单域防腐层服务接口（卖家店铺域视角）。
/// 仅暴露卖家归属校验所需的订单域查询能力，屏蔽订单域内部模型。
/// 接口定义在应用层，实现位于基础设施层（GrpcOrderAntiCorruptionClient）。
/// </summary>
public interface IOrderAntiCorruptionService
{
    /// <summary>
    /// 按订单标识反查其归属卖家标识。
    /// 用于卖家资源归属校验（resourceType=order）：比对调用方声明的 sellerId 与订单实际归属卖家。
    /// </summary>
    /// <param name="orderId">订单标识。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>订单归属卖家标识；订单不存在或防腐层调用失败时返回 null（fail-closed，由调用方判 false）。</returns>
    Task<Guid?> GetOrderSellerIdAsync(Guid orderId, CancellationToken ct = default);

    /// <summary>
    /// 按店铺与日期范围查询销售汇总数据（用于数据导出 SalesSummary 报表）。
    /// 经 gRPC 调订单域聚合销售指标，返回表头与行数据以供文件生成器渲染。
    /// 实现位于 Task 13（GrpcOrderAntiCorruptionClient）。
    /// </summary>
    /// <param name="shopId">店铺标识。</param>
    /// <param name="startDate">起始日期（UTC）。</param>
    /// <param name="endDate">结束日期（UTC）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>表头列表与行数据列表；ACL 调用失败时返回空列表（fail-soft）。</returns>
    Task<(List<string> Headers, List<IReadOnlyDictionary<string, object?>> Rows)> GetSalesSummaryAsync(
        Guid shopId, DateTime startDate, DateTime endDate, CancellationToken ct = default);

    /// <summary>
    /// 按店铺与日期范围查询订单明细数据（用于数据导出 OrderDetail 报表）。
    /// 经 gRPC 调订单域分页拉取订单明细，返回表头与行数据以供文件生成器渲染。
    /// 实现位于 Task 13（GrpcOrderAntiCorruptionClient）。
    /// </summary>
    /// <param name="shopId">店铺标识。</param>
    /// <param name="startDate">起始日期（UTC）。</param>
    /// <param name="endDate">结束日期（UTC）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>表头列表与行数据列表；ACL 调用失败时返回空列表（fail-soft）。</returns>
    Task<(List<string> Headers, List<IReadOnlyDictionary<string, object?>> Rows)> GetOrderDetailForExportAsync(
        Guid shopId, DateTime startDate, DateTime endDate, CancellationToken ct = default);
}
