using Leno.Payment.Application.DTOs;
using Leno.Payment.Domain.ValueObjects;

namespace Leno.Payment.Application;

/// <summary>
/// 支付应用服务，编排发起支付、支付结果查询、渠道状态主动查询与运营管理用例。
/// </summary>
public interface IPaymentAppService
{
    /// <summary>
    /// 同步发起支付（spec F-PAY-001）。
    /// 经防腐层校验订单存在性、买家归属与可支付状态，校验支付金额与订单应付一致（INV-PAY-01），
    /// 创建支付单（Pending 态）后落库，调用渠道适配器 CreatePaymentAsync 取得预支付参数，
    /// 更新支付单为 ChannelOrdered 态（或渠道下单失败时 MarkFailed），同事务经发件箱发布领域事件。
    /// 同步返回调起参数（prepayId/codeUrl/h5Url）供前端调起微信收银台或跳转支付宝。
    /// </summary>
    /// <param name="currentUserId">当前登录买家标识（来自 JWT），用于校验订单归属。</param>
    /// <param name="request">发起支付请求，含订单标识、渠道、场景、幂等键。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>调起参数 DTO，含支付单标识、调起参数与过期时间。</returns>
    /// <exception cref="Domain.Exceptions.PaymentDomainException">订单不存在、订单不属于当前买家、订单不可支付、渠道未注册时抛出。</exception>
    /// <exception cref="Domain.Exceptions.PaymentAlreadySucceededException">订单已由其他支付单完成支付时抛出。</exception>
    Task<PaymentInitiationResultDto> CreatePaymentAsync(Guid currentUserId, CreatePaymentRequest request, CancellationToken ct = default);

    /// <summary>
    /// 按订单标识查询支付结果（含渠道信息）。
    /// </summary>
    Task<PaymentOrderDto?> GetPaymentResultAsync(Guid orderId, CancellationToken ct = default);

    /// <summary>
    /// 主动查询渠道支付状态，若已支付则补偿更新支付单并返回最新状态。
    /// </summary>
    Task<ChannelStatusDto> QueryPaymentStatusAsync(Guid paymentId, CancellationToken ct = default);

    /// <summary>
    /// 运营端分页查询全平台支付记录。
    /// </summary>
    Task<PaymentListResultDto> QueryPaymentsAsync(Guid? userId, PaymentChannel? channel, PaymentStatus? status, DateTime? startDate, DateTime? endDate, int page, int pageSize, CancellationToken ct = default);
}

/// <summary>
/// 退款应用服务，编排退款结果查询与运营管理用例。
/// </summary>
public interface IRefundAppService
{
    /// <summary>
    /// 按售后单标识查询退款结果。
    /// </summary>
    Task<RefundOrderDto?> GetRefundResultAsync(Guid afterSalesId, CancellationToken ct = default);

    /// <summary>
    /// 运营端分页查询全平台退款记录。
    /// </summary>
    Task<RefundListResultDto> QueryRefundsAsync(Guid? orderId, RefundStatus? status, int page, int pageSize, CancellationToken ct = default);
}
