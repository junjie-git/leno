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
}
