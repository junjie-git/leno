using Leno.Order.Domain.Exceptions;
using Leno.Order.Domain.ValueObjects;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;

namespace Leno.Order.Domain.Aggregates;

/// <summary>
/// 订单聚合根，封装订单金额不变量与状态机。
/// 金额不变量：<see cref="TotalAmount"/> = <see cref="ItemsAmount"/> - <see cref="DiscountAmount"/> - <see cref="PointsOffsetAmount"/> + <see cref="FreightAmount"/>。
/// 状态流转：PendingPayment → Paid → Shipped → Completed → Closed；PendingPayment/Paid/Shipped → Cancelled。
/// 聚合标识 <see cref="Entity.Id"/> 即对外 <c>OrderId</c>。
/// </summary>
public sealed class Order : AggregateRoot
{
    /// <summary>订单编号（业务可读，全局唯一）。</summary>
    public string OrderNo { get; private set; } = string.Empty;

    /// <summary>订单类型。</summary>
    public OrderType OrderType { get; private set; }

    /// <summary>买家账号标识。</summary>
    public Guid UserId { get; private set; }

    /// <summary>卖家（店铺）标识，语义等同卖家与店铺管理域的 ShopId。</summary>
    public Guid SellerId { get; private set; }

    /// <summary>
    /// 订单明细集合，仅经聚合根维护。
    /// 持久化为聚合子实体集合，故以可赋值 List 暴露给 EF Core，私有 setter 阻止外部整体替换。
    /// </summary>
    public List<OrderItem> Items { get; private set; } = new();

    /// <summary>商品总金额（明细小计之和）。</summary>
    public decimal ItemsAmount { get; private set; }

    /// <summary>优惠总金额（明细分摊之和）。</summary>
    public decimal DiscountAmount { get; private set; }

    /// <summary>积分抵现金额。</summary>
    public decimal PointsOffsetAmount { get; private set; }

    /// <summary>运费金额。</summary>
    public decimal FreightAmount { get; private set; }

    /// <summary>订单总金额（实付）。</summary>
    public decimal TotalAmount { get; private set; }

    /// <summary>订单状态。</summary>
    public OrderStatus Status { get; private set; }

    /// <summary>收货地址快照，EF Core 作为 owned type 映射。</summary>
    public AddressSnapshot AddressSnapshot { get; private set; } = null!;

    /// <summary>支付方式，下单未支付时为空。</summary>
    public PaymentMethod? PaymentMethod { get; private set; }

    /// <summary>支付截止时间（UTC），超时自动取消。</summary>
    public DateTime ExpireAt { get; private set; }

    /// <summary>支付时间（UTC）。</summary>
    public DateTime? PaidAt { get; private set; }

    /// <summary>支付单标识。</summary>
    public Guid? PaymentId { get; private set; }

    /// <summary>第三方交易号。</summary>
    public string? TradeNo { get; private set; }

    /// <summary>发货时间（UTC）。</summary>
    public DateTime? ShippedAt { get; private set; }

    /// <summary>物流单号。</summary>
    public string? LogisticsNo { get; private set; }

    /// <summary>完成时间（UTC）。</summary>
    public DateTime? CompletedAt { get; private set; }

    /// <summary>售后窗口结束时间（UTC），完成后 7 天。</summary>
    public DateTime? AfterSalesWindowEndsAt { get; private set; }

    /// <summary>取消时间（UTC）。</summary>
    public DateTime? CancelledAt { get; private set; }

    /// <summary>取消原因。</summary>
    public string? CancelReason { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private Order() { }

    private Order(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，校验入参合法、计算金额不变量、置待支付态并发布 <see cref="OrderCreatedEvent"/>。
    /// </summary>
    /// <param name="orderId">订单标识，由应用层生成。</param>
    /// <param name="orderNo">订单编号。</param>
    /// <param name="orderType">订单类型。</param>
    /// <param name="userId">买家标识。</param>
    /// <param name="sellerId">卖家标识。</param>
    /// <param name="items">订单明细列表，须非空。</param>
    /// <param name="address">收货地址快照。</param>
    /// <param name="freightAmount">运费金额。</param>
    /// <param name="pointsOffsetAmount">积分抵现金额，须 ≥ 0 且 ≤ 商品总金额。</param>
    /// <param name="expireAt">支付截止时间（UTC），须晚于当前时间。</param>
    public static Order Create(
        Guid orderId,
        string orderNo,
        OrderType orderType,
        Guid userId,
        Guid sellerId,
        List<OrderItem> items,
        AddressSnapshot address,
        decimal freightAmount,
        decimal pointsOffsetAmount,
        DateTime expireAt)
    {
        if (orderId == Guid.Empty)
        {
            throw new OrderDomainException("OrderId 不可为空", "ORDER_ID_EMPTY");
        }

        if (string.IsNullOrWhiteSpace(orderNo))
        {
            throw new OrderDomainException("订单编号不可为空", "ORDER_NO_EMPTY");
        }

        if (userId == Guid.Empty)
        {
            throw new OrderDomainException("UserId 不可为空", "ORDER_USER_EMPTY");
        }

        if (sellerId == Guid.Empty)
        {
            throw new OrderDomainException("SellerId 不可为空", "ORDER_SELLER_EMPTY");
        }

        if (items is null || items.Count == 0)
        {
            throw new OrderDomainException("订单明细不可为空", "ORDER_ITEMS_EMPTY");
        }

        if (address is null)
        {
            throw new OrderDomainException("收货地址不可为空", "ORDER_ADDRESS_EMPTY");
        }

        if (expireAt <= DateTime.UtcNow)
        {
            throw new OrderDomainException("支付截止时间须晚于当前时间", "ORDER_EXPIRE_INVALID");
        }

        if (freightAmount < 0)
        {
            throw new OrderDomainException("运费金额不可为负", "ORDER_FREIGHT_INVALID");
        }

        var itemsAmount = items.Sum(i => i.Subtotal);

        if (pointsOffsetAmount < 0 || pointsOffsetAmount > itemsAmount)
        {
            throw new OrderDomainException(
                $"积分抵现金额非法：抵现 {pointsOffsetAmount}，商品总额 {itemsAmount}",
                "ORDER_POINTS_OFFSET_INVALID");
        }

        var order = new Order(orderId)
        {
            OrderNo = orderNo,
            OrderType = orderType,
            UserId = userId,
            SellerId = sellerId,
            Items = items,
            ItemsAmount = itemsAmount,
            DiscountAmount = 0,
            PointsOffsetAmount = pointsOffsetAmount,
            FreightAmount = freightAmount,
            Status = OrderStatus.PendingPayment,
            AddressSnapshot = address,
            ExpireAt = expireAt
        };
        order.RecalculateTotal();

        var sourceCartItemIds = items
            .Where(i => i.SourceCartItemId.HasValue)
            .Select(i => i.SourceCartItemId!.Value)
            .ToList();

        order.AddDomainEvent(new OrderCreatedEvent(orderId, userId, order.TotalAmount, "CNY", DateTime.UtcNow, sourceCartItemIds));

        return order;
    }

    /// <summary>
    /// 应用优惠分摊，校验待支付态、按 SKU 定位明细并校验分摊额度，
    /// 汇总分摊为 <see cref="DiscountAmount"/> 并重算 <see cref="TotalAmount"/>。
    /// </summary>
    /// <param name="discountAllocations">按 SKU 的优惠分摊列表。</param>
    public void ApplyDiscount(List<(Guid SkuId, decimal Allocation)> discountAllocations)
    {
        if (Status != OrderStatus.PendingPayment)
        {
            throw new OrderDomainException(
                $"当前状态 {Status} 不可应用优惠，仅 PendingPayment 可应用",
                "ORDER_DISCOUNT_STATUS_INVALID");
        }

        if (discountAllocations is null || discountAllocations.Count == 0)
        {
            throw new OrderDomainException("优惠分摊列表不可为空", "ORDER_DISCOUNT_ALLOCATIONS_EMPTY");
        }

        decimal totalDiscount = 0;
        foreach (var (skuId, allocation) in discountAllocations)
        {
            var item = Items.FirstOrDefault(i => i.SkuId == skuId);
            if (item is null)
            {
                throw new OrderDomainException(
                    $"订单明细中不存在 SKU {skuId}",
                    "ORDER_DISCOUNT_SKU_NOT_FOUND",
                    404);
            }

            if (allocation < 0 || allocation > item.Subtotal)
            {
                throw new OrderDomainException(
                    $"优惠分摊金额非法：SKU {skuId}，分摊 {allocation}，小计 {item.Subtotal}",
                    "ORDER_DISCOUNT_ALLOCATION_INVALID");
            }

            item.ApplyDiscount(allocation);
            totalDiscount += allocation;
        }

        DiscountAmount = totalDiscount;
        RecalculateTotal();
    }

    /// <summary>
    /// 应用积分抵现，校验 0 ≤ 抵现 ≤ 商品总额 - 已享优惠，更新 <see cref="PointsOffsetAmount"/> 并重算 <see cref="TotalAmount"/>。
    /// </summary>
    /// <param name="pointsOffsetAmount">积分抵现金额。</param>
    public void ApplyPointsOffset(decimal pointsOffsetAmount)
    {
        if (Status != OrderStatus.PendingPayment)
        {
            throw new OrderDomainException(
                $"当前状态 {Status} 不可应用积分抵现，仅 PendingPayment 可应用",
                "ORDER_POINTS_OFFSET_STATUS_INVALID");
        }

        var maxOffset = ItemsAmount - DiscountAmount;
        if (pointsOffsetAmount < 0 || pointsOffsetAmount > maxOffset)
        {
            throw new OrderDomainException(
                $"积分抵现金额非法：抵现 {pointsOffsetAmount}，可抵上限 {maxOffset}",
                "ORDER_POINTS_OFFSET_INVALID");
        }

        PointsOffsetAmount = pointsOffsetAmount;
        RecalculateTotal();
    }

    /// <summary>
    /// 标记支付成功，校验待支付态，置已支付态并发布 <see cref="OrderPaidEvent"/>。
    /// </summary>
    /// <param name="paymentId">支付单标识。</param>
    /// <param name="channel">支付渠道。</param>
    /// <param name="paidAt">支付时间（UTC）。</param>
    /// <param name="tradeNo">第三方交易号。</param>
    public void MarkAsPaid(Guid paymentId, string channel, DateTime paidAt, string tradeNo)
    {
        if (Status != OrderStatus.PendingPayment)
        {
            throw new OrderDomainException(
                $"当前状态 {Status} 不可标记支付，仅 PendingPayment 可支付",
                "ORDER_PAID_STATUS_INVALID");
        }

        Status = OrderStatus.Paid;
        PaymentId = paymentId;
        PaidAt = paidAt;
        TradeNo = tradeNo;
        AddDomainEvent(new OrderPaidEvent(Id, UserId, paymentId, channel, paidAt, tradeNo, TotalAmount, "CNY"));
    }

    /// <summary>
    /// 发货，校验已支付态与物流单号非空，置已发货态并发布 <see cref="OrderShippedEvent"/>。
    /// </summary>
    /// <param name="logisticsNo">物流单号。</param>
    /// <param name="shippedAt">发货时间（UTC）。</param>
    /// <param name="operatorId">操作人标识（审计用）。</param>
    public void Ship(string logisticsNo, DateTime shippedAt, Guid operatorId)
    {
        if (Status != OrderStatus.Paid)
        {
            throw new OrderDomainException(
                $"当前状态 {Status} 不可发货，仅 Paid 可发货",
                "ORDER_SHIP_STATUS_INVALID");
        }

        if (string.IsNullOrWhiteSpace(logisticsNo))
        {
            throw new OrderDomainException("物流单号不可为空", "ORDER_LOGISTICS_NO_EMPTY");
        }

        Status = OrderStatus.Shipped;
        LogisticsNo = logisticsNo;
        ShippedAt = shippedAt;
        AddDomainEvent(new OrderShippedEvent(Id, UserId, SellerId, logisticsNo, shippedAt));
    }

    /// <summary>
    /// 确认收货，校验已发货态，置已完成态、设置 7 天售后窗口并发布 <see cref="OrderCompletedEvent"/>。
    /// </summary>
    public void ConfirmReceipt()
    {
        if (Status != OrderStatus.Shipped)
        {
            throw new OrderDomainException(
                $"当前状态 {Status} 不可确认收货，仅 Shipped 可确认",
                "ORDER_CONFIRM_STATUS_INVALID");
        }

        Status = OrderStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        AfterSalesWindowEndsAt = CompletedAt.Value.AddDays(7);
        AddDomainEvent(new OrderCompletedEvent(Id, UserId, SellerId, TotalAmount, "CNY", CompletedAt.Value));
    }

    /// <summary>
    /// 完成会员套餐订单，校验已支付且为会员订单，置已完成态（无售后窗口）并发布 <see cref="OrderCompletedEvent"/>。
    /// </summary>
    public void CompleteMembershipOrder()
    {
        if (Status != OrderStatus.Paid)
        {
            throw new OrderDomainException(
                $"当前状态 {Status} 不可完成会员订单，仅 Paid 可完成",
                "ORDER_MEMBERSHIP_COMPLETE_STATUS_INVALID");
        }

        if (OrderType != OrderType.Membership)
        {
            throw new OrderDomainException("仅会员套餐订单可调用完成会员订单", "ORDER_MEMBERSHIP_TYPE_INVALID");
        }

        Status = OrderStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        AfterSalesWindowEndsAt = CompletedAt.Value;
        AddDomainEvent(new OrderCompletedEvent(Id, UserId, SellerId, TotalAmount, "CNY", CompletedAt.Value));
    }

    /// <summary>
    /// 关闭售后窗口，校验已完成态，置已关闭态并发布 <see cref="OrderAfterSalesWindowClosedEvent"/>。
    /// </summary>
    public void CloseAfterSalesWindow()
    {
        if (Status != OrderStatus.Completed)
        {
            throw new OrderDomainException(
                $"当前状态 {Status} 不可关闭售后窗口，仅 Completed 可关闭",
                "ORDER_CLOSE_STATUS_INVALID");
        }

        Status = OrderStatus.Closed;
        AddDomainEvent(new OrderAfterSalesWindowClosedEvent(Id, UserId, AfterSalesWindowEndsAt!.Value));
    }

    /// <summary>
    /// 取消订单（待支付态，买家主动或超时自动），校验待支付态，置已取消态并发布 <see cref="OrderCancelledEvent"/>（含释放冻结积分）。
    /// </summary>
    /// <param name="reason">取消原因。</param>
    /// <param name="cancelledBy">取消方（Buyer/System）。</param>
    public void Cancel(string reason, string cancelledBy)
    {
        if (Status != OrderStatus.PendingPayment)
        {
            throw new OrderDomainException(
                $"当前状态 {Status} 不可取消，仅 PendingPayment 可取消",
                "ORDER_CANCEL_STATUS_INVALID");
        }

        Status = OrderStatus.Cancelled;
        CancelReason = reason;
        CancelledAt = DateTime.UtcNow;
        AddDomainEvent(new OrderCancelledEvent(Id, reason, CancelledAt.Value, cancelledBy, (int)Math.Round(PointsOffsetAmount * 100)));
    }

    /// <summary>
    /// 强制取消异常订单（已支付或已发货态，运营介入），校验状态合法，置已取消态并发布 <see cref="OrderCancelledEvent"/>。
    /// </summary>
    /// <param name="reason">取消原因。</param>
    /// <param name="operatorId">操作人标识。</param>
    public void ForceCancel(string reason, string operatorId)
    {
        if (Status != OrderStatus.Paid && Status != OrderStatus.Shipped)
        {
            throw new OrderDomainException(
                $"当前状态 {Status} 不可强制取消，仅 Paid/Shipped 可强制取消",
                "ORDER_FORCE_CANCEL_STATUS_INVALID");
        }

        Status = OrderStatus.Cancelled;
        CancelReason = reason;
        CancelledAt = DateTime.UtcNow;
        AddDomainEvent(new OrderCancelledEvent(Id, reason, CancelledAt.Value, operatorId, (int)Math.Round(PointsOffsetAmount * 100)));
    }

    /// <summary>
    /// 重算订单总金额，强制金额不变量：TotalAmount = ItemsAmount - DiscountAmount - PointsOffsetAmount + FreightAmount。
    /// </summary>
    private void RecalculateTotal()
    {
        TotalAmount = ItemsAmount - DiscountAmount - PointsOffsetAmount + FreightAmount;
    }
}
