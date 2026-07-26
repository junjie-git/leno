using Leno.ReviewAfterSales.Application.DTOs;
using Leno.ReviewAfterSales.Domain.ValueObjects;

namespace Leno.ReviewAfterSales.Application;

/// <summary>
/// 售后应用服务接口，编排售后申请、审核、撤销、退货、确认收货与查询用例。
/// 审核通过时发布 <c>RefundRequestedIntegrationEvent</c> 请求支付域执行退款。
/// </summary>
public interface IAfterSalesAppService
{
    /// <summary>买家提交售后申请，校验资格后创建售后单聚合。</summary>
    Task<AfterSalesDto> SubmitAfterSalesAsync(Guid userId, SubmitAfterSalesDto dto, CancellationToken ct = default);

    /// <summary>卖家审核同意售后，置已同意态。</summary>
    Task ApproveAfterSalesAsync(Guid afterSalesId, Guid operatorId, decimal approvedAmount, CancellationToken ct = default);

    /// <summary>卖家驳回售后，附驳回原因。</summary>
    Task RejectAfterSalesAsync(Guid afterSalesId, Guid operatorId, string reason, CancellationToken ct = default);

    /// <summary>卖家确认收到退货，置已确认收货态。</summary>
    Task ConfirmReturnAsync(Guid afterSalesId, Guid operatorId, CancellationToken ct = default);

    /// <summary>运营审核同意售后，进入退款流程。</summary>
    Task AdminApproveAfterSalesAsync(Guid afterSalesId, Guid operatorId, decimal approvedAmount, CancellationToken ct = default);

    /// <summary>运营驳回售后，附驳回原因。</summary>
    Task AdminRejectAfterSalesAsync(Guid afterSalesId, Guid operatorId, string reason, CancellationToken ct = default);

    /// <summary>买家退货并填写物流单号，置已退货态。</summary>
    Task ReturnGoodsAsync(Guid afterSalesId, Guid userId, string trackingNo, CancellationToken ct = default);

    /// <summary>买家撤销售后申请，仅待审核态可撤销。</summary>
    Task CancelAfterSalesAsync(Guid afterSalesId, Guid userId, string reason, CancellationToken ct = default);

    /// <summary>买家端按订单查询售后单列表。</summary>
    Task<List<AfterSalesDto>> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default);

    /// <summary>
    /// 买家端按订单查询售后单列表，校验当前用户为订单归属买家。
    /// 通过订单域防腐层反查订单 UserId，非归属买家抛 <c>AFTERSALES_FORBIDDEN</c>。
    /// </summary>
    Task<List<AfterSalesDto>> GetByOrderIdForUserAsync(Guid orderId, Guid userId, CancellationToken ct = default);

    /// <summary>买家端分页查询我的售后单。</summary>
    Task<AfterSalesListResultDto> GetByUserAsync(Guid userId, int page, int pageSize, CancellationToken ct = default);

    /// <summary>卖家端分页查询收到的售后单。</summary>
    Task<AfterSalesListResultDto> GetBySellerAsync(Guid sellerId, AfterSalesStatus? status, int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// 卖家端按售后单标识查询详情，校验当前用户为售后单归属卖家。
    /// 通过聚合根 <see cref="Domain.Aggregates.AfterSales.SellerId"/> 与传入 sellerId 比对，
    /// 非归属卖家抛 <c>AFTERSALES_NOT_OWNED</c>，售后单不存在抛 <see cref="InvalidOperationException"/>。
    /// </summary>
    /// <param name="afterSalesId">售后单标识。</param>
    /// <param name="sellerId">当前卖家标识，从 JWT 注入。</param>
    /// <returns>售后单 DTO。</returns>
    Task<AfterSalesDto> GetByIdForSellerAsync(Guid afterSalesId, Guid sellerId, CancellationToken ct = default);

    /// <summary>运营端分页查询全平台售后单。</summary>
    Task<AfterSalesListResultDto> QueryAsync(Guid? orderId, Guid? userId, Guid? sellerId, AfterSalesStatus? status, int page, int pageSize, CancellationToken ct = default);
}
