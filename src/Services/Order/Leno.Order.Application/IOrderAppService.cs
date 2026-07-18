using Leno.Order.Application.DTOs;
using Leno.Order.Domain.ValueObjects;

namespace Leno.Order.Application;

/// <summary>
/// 订单应用服务，编排下单、支付、发货、确认收货、取消与查询用例。
/// </summary>
public interface IOrderAppService
{
    /// <summary>
    /// 创建订单（按卖家自动拆单）。
    /// </summary>
    /// <param name="userId">买家标识。</param>
    /// <param name="dto">创建订单入参。</param>
    Task<OrderDto> CreateOrderAsync(Guid userId, CreateOrderDto dto, CancellationToken ct = default);

    /// <summary>
    /// 立即购买（单 SKU，内部转换为创建订单）。
    /// </summary>
    /// <param name="userId">买家标识。</param>
    /// <param name="dto">立即购买入参。</param>
    Task<OrderDto> BuyNowAsync(Guid userId, BuyNowDto dto, CancellationToken ct = default);

    /// <summary>
    /// 下单预览，计算预估金额不落库。
    /// </summary>
    /// <param name="userId">买家标识。</param>
    /// <param name="dto">创建订单入参。</param>
    Task<OrderPreviewResultDto> PreviewAsync(Guid userId, CreateOrderDto dto, CancellationToken ct = default);

    /// <summary>
    /// 发起支付，发布支付请求集成事件。
    /// </summary>
    /// <param name="orderId">订单标识。</param>
    /// <param name="userId">买家标识。</param>
    /// <param name="dto">支付入参。</param>
    Task PayAsync(Guid orderId, Guid userId, PayOrderDto dto, CancellationToken ct = default);

    /// <summary>
    /// 发货。
    /// </summary>
    /// <param name="orderId">订单标识。</param>
    /// <param name="operatorId">操作人标识（卖家，审计用）。</param>
    /// <param name="dto">发货入参。</param>
    Task ShipAsync(Guid orderId, Guid operatorId, ShipOrderDto dto, CancellationToken ct = default);

    /// <summary>
    /// 查询订单物流轨迹。
    /// </summary>
    /// <param name="orderId">订单标识。</param>
    [Obsolete("请使用 IQueryHandler<LogisticsTraceQuery, LogisticsTraceResult>，将在 2026-08-01 移除")]
    Task<LogisticsTrackingDto> GetLogisticsTraceAsync(Guid orderId, CancellationToken ct = default);

    /// <summary>
    /// 确认收货。
    /// </summary>
    /// <param name="orderId">订单标识。</param>
    /// <param name="userId">买家标识。</param>
    Task ConfirmReceiptAsync(Guid orderId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// 买家取消订单（待支付态）。
    /// </summary>
    /// <param name="orderId">订单标识。</param>
    /// <param name="userId">买家标识。</param>
    /// <param name="dto">取消入参。</param>
    Task CancelAsync(Guid orderId, Guid userId, CancelOrderDto dto, CancellationToken ct = default);

    /// <summary>
    /// 运营强制取消订单（待支付/已支付/已发货态）。
    /// </summary>
    /// <param name="orderId">订单标识。</param>
    /// <param name="operatorId">操作人标识（运营人员）。</param>
    /// <param name="dto">强制取消入参。</param>
    Task ForceCancelAsync(Guid orderId, Guid operatorId, ForceCancelOrderDto dto, CancellationToken ct = default);

    /// <summary>
    /// 按标识查询订单。
    /// </summary>
    /// <param name="orderId">订单标识。</param>
    [Obsolete("请使用 IQueryHandler<OrderDetailQuery, OrderDetailResult>，将在 2026-08-01 移除")]
    Task<OrderDto> GetByIdAsync(Guid orderId, CancellationToken ct = default);

    /// <summary>
    /// 分页条件查询订单列表。
    /// </summary>
    /// <param name="userId">买家标识过滤，为空不过滤。</param>
    /// <param name="sellerId">卖家标识过滤，为空不过滤。</param>
    /// <param name="status">订单状态过滤，为空不过滤。</param>
    /// <param name="page">页码（从 1 起）。</param>
    /// <param name="pageSize">每页大小。</param>
    [Obsolete("请使用 IQueryHandler<OrderListQuery, OrderListResult>，将在 2026-08-01 移除")]
    Task<OrderListResultDto> QueryAsync(Guid? userId, Guid? sellerId, OrderStatus? status, int page, int pageSize, CancellationToken ct = default);
}
