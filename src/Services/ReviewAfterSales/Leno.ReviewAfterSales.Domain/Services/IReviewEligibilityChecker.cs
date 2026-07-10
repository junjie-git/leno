namespace Leno.ReviewAfterSales.Domain.Services;

/// <summary>
/// 评价资格校验器接口，供应用层在调用 Review.Create 前校验订单已完成、订单行未重复评价、
/// 在评价期限内且申请人为订单买家等不变量。实现位于基础设施层，通过订单域防腐层查询。
/// </summary>
public interface IReviewEligibilityChecker
{
    /// <summary>
    /// 校验订单已完成、订单行未评价、在评价期限内且申请人为订单买家。
    /// 校验失败抛出 <see cref="Leno.SharedKernel.Exceptions.DomainException"/>。
    /// </summary>
    /// <param name="orderId">订单标识。</param>
    /// <param name="orderLineId">订单行标识。</param>
    /// <param name="userId">申请人标识。</param>
    Task EnsureEligibleAsync(Guid orderId, Guid orderLineId, Guid userId, CancellationToken ct = default);
}
