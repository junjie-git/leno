using Leno.ReviewAfterSales.Domain.ValueObjects;

namespace Leno.ReviewAfterSales.Domain.Services;

/// <summary>
/// 售后资格校验器接口，供应用层在调用 AfterSales.Create 前校验售后期限内、
/// 同订单行无进行中同类型售后单且申请人为订单买家等不变量。实现位于基础设施层，通过订单域防腐层查询。
/// </summary>
public interface IAfterSalesEligibilityChecker
{
    /// <summary>
    /// 校验售后申请在售后期限内、同订单行无进行中同类型售后单且申请人为订单买家。
    /// 校验失败抛出 <see cref="Leno.SharedKernel.Exceptions.DomainException"/>。
    /// 校验通过后返回订单状态概要（含订单域真实 SellerId），供应用层创建售后单时使用，
    /// 避免应用层信任客户端提交的 SellerId。
    /// </summary>
    /// <param name="orderId">订单标识。</param>
    /// <param name="orderLineId">订单行标识，整单售后可空。</param>
    /// <param name="userId">申请人标识。</param>
    /// <param name="type">售后类型。</param>
    /// <returns>校验通过的订单状态概要，包含订单域真实 SellerId。</returns>
    Task<OrderStatusInfo> EnsureEligibleAsync(Guid orderId, Guid? orderLineId, Guid userId, AfterSalesType type, CancellationToken ct = default);
}
