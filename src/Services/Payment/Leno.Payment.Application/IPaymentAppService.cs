using Leno.Payment.Application.DTOs;
using Leno.Payment.Domain.ValueObjects;

namespace Leno.Payment.Application;

/// <summary>
/// 支付应用服务，编排支付结果查询、渠道状态主动查询与运营管理用例。
/// </summary>
public interface IPaymentAppService
{
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
