using Leno.Payment.Domain.Events;
using Leno.Payment.Domain.Exceptions;
using Leno.Payment.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.Payment.Domain.Aggregates;

/// <summary>
/// 支付单聚合根，封装支付单金额、渠道与状态机。
/// 状态流转：Pending → ChannelOrdered → Paid；Pending/ChannelOrdered → Failed；Pending/ChannelOrdered/Failed → Closed。
/// 聚合标识 <see cref="Entity.Id"/> 即对外 <c>PaymentId</c>。
/// </summary>
public sealed class PaymentOrder : AggregateRoot
{
    /// <summary>商户支付单号（业务可读，全局唯一），传给第三方渠道作为 out_trade_no。</summary>
    public string OutTradeNo { get; private set; } = string.Empty;

    /// <summary>关联订单标识。</summary>
    public Guid OrderId { get; private set; }

    /// <summary>买家账号标识。</summary>
    public Guid UserId { get; private set; }

    /// <summary>支付金额。</summary>
    public decimal Amount { get; private set; }

    /// <summary>币种（ISO 4217），默认 CNY。</summary>
    public string Currency { get; private set; } = "CNY";

    /// <summary>支付渠道。</summary>
    public PaymentChannel Channel { get; private set; }

    /// <summary>第三方交易号（渠道返回）。</summary>
    public string? ChannelTradeNo { get; private set; }

    /// <summary>支付单状态。</summary>
    public PaymentStatus Status { get; private set; }

    /// <summary>预支付标识（微信预支付会话标识）。</summary>
    public string? PrepayId { get; private set; }

    /// <summary>扫码支付链接（微信 Native / 支付宝当面付）。</summary>
    public string? CodeUrl { get; private set; }

    /// <summary>H5 支付跳转链接。</summary>
    public string? H5Url { get; private set; }

    /// <summary>支付截止时间（UTC），超时关闭。</summary>
    public DateTime ExpireAt { get; private set; }

    /// <summary>支付时间（UTC）。</summary>
    public DateTime? PaidAt { get; private set; }

    /// <summary>失败原因。</summary>
    public string? FailReason { get; private set; }

    /// <summary>
    /// 乐观并发令牌（rowversion），由数据库自动维护。
    /// EF Core 通过 <c>IsRowVersion()</c> 标记为并发令牌，更新时校验版本号，
    /// 防止异步通知与补偿任务并发更新同一支付单时发生覆盖。
    /// </summary>
    public byte[]? RowVersion { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private PaymentOrder() { }

    private PaymentOrder(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，校验入参合法、生成商户支付单号、置待支付态并设置 2 小时过期。
    /// </summary>
    /// <param name="paymentId">支付单标识，由应用层生成。</param>
    /// <param name="orderId">关联订单标识。</param>
    /// <param name="userId">买家标识。</param>
    /// <param name="amount">支付金额，须 &gt; 0。</param>
    /// <param name="currency">币种，为空默认 CNY。</param>
    /// <param name="channel">支付渠道。</param>
    public static PaymentOrder Create(
        Guid paymentId,
        Guid orderId,
        Guid userId,
        decimal amount,
        string currency,
        PaymentChannel channel)
    {
        if (paymentId == Guid.Empty)
        {
            throw new PaymentDomainException("PaymentId 不可为空", "PAYMENT_ID_EMPTY");
        }

        if (orderId == Guid.Empty)
        {
            throw new PaymentDomainException("OrderId 不可为空", "PAYMENT_ORDER_EMPTY");
        }

        if (userId == Guid.Empty)
        {
            throw new PaymentDomainException("UserId 不可为空", "PAYMENT_USER_EMPTY");
        }

        if (amount <= 0)
        {
            throw new PaymentDomainException("支付金额须大于 0", "PAYMENT_AMOUNT_INVALID");
        }

        return new PaymentOrder(paymentId)
        {
            // P2-16：原时间戳+6位随机数在高并发同秒内碰撞概率 1/900000，
            // 改为时间戳+GUID(N 格式 32 位)，由 GUID 全局唯一性消除碰撞。
            // 总长 3+14+32=49，不超过 out_trade_no 列 MaxLength=64。
            OutTradeNo = $"PAY{DateTime.UtcNow:yyyyMMddHHmmss}{Guid.NewGuid():N}",
            OrderId = orderId,
            UserId = userId,
            Amount = amount,
            Currency = string.IsNullOrWhiteSpace(currency) ? "CNY" : currency,
            Channel = channel,
            Status = PaymentStatus.Pending,
            ExpireAt = DateTime.UtcNow.AddHours(2)
        };
    }

    /// <summary>
    /// 标记渠道已下单，校验待支付态，置渠道已下单态并记录渠道返回的预支付参数。
    /// </summary>
    /// <param name="channelTradeNo">第三方交易号。</param>
    /// <param name="prepayId">预支付标识，可空。</param>
    /// <param name="codeUrl">扫码支付链接，可空。</param>
    /// <param name="h5Url">H5 支付跳转链接，可空。</param>
    public void MarkChannelOrdered(string channelTradeNo, string? prepayId, string? codeUrl, string? h5Url)
    {
        if (Status != PaymentStatus.Pending)
        {
            throw new PaymentDomainException(
                $"当前状态 {Status} 不可标记渠道下单，仅 Pending 可标记",
                "PAYMENT_CHANNEL_ORDER_STATUS_INVALID");
        }

        if (string.IsNullOrWhiteSpace(channelTradeNo))
        {
            throw new PaymentDomainException("第三方交易号不可为空", "PAYMENT_CHANNEL_TRADE_NO_EMPTY");
        }

        Status = PaymentStatus.ChannelOrdered;
        ChannelTradeNo = channelTradeNo;
        PrepayId = prepayId;
        CodeUrl = codeUrl;
        H5Url = h5Url;
    }

    /// <summary>
    /// 标记支付成功，校验状态合法（待支付或渠道已下单）且实付金额与本地金额一致，置已支付态并发布 <see cref="PaymentSucceededDomainEvent"/>。
    /// </summary>
    /// <param name="channelTradeNo">第三方交易号。</param>
    /// <param name="amount">渠道回调/查询解析的实付金额（元），须与 <see cref="Amount"/> 一致。</param>
    /// <param name="paidAt">支付时间（UTC）。</param>
    public void MarkSucceeded(string channelTradeNo, decimal amount, DateTime paidAt)
    {
        if (Status != PaymentStatus.Pending && Status != PaymentStatus.ChannelOrdered)
        {
            throw new PaymentDomainException(
                $"当前状态 {Status} 不可标记支付成功，仅 Pending/ChannelOrdered 可标记",
                "PAYMENT_PAID_STATUS_INVALID");
        }

        if (string.IsNullOrWhiteSpace(channelTradeNo))
        {
            throw new PaymentDomainException("第三方交易号不可为空", "PAYMENT_CHANNEL_TRADE_NO_EMPTY");
        }

        if (amount != Amount)
        {
            throw new PaymentDomainException(
                $"支付金额不一致，期望 {Amount} 元，实付 {amount} 元",
                "PAYMENT_AMOUNT_MISMATCH");
        }

        Status = PaymentStatus.Paid;
        ChannelTradeNo = channelTradeNo;
        PaidAt = paidAt;
        AddDomainEvent(new PaymentSucceededDomainEvent(OrderId, Id, UserId, Channel.ToString(), channelTradeNo, Amount, Currency, paidAt));
    }

    /// <summary>
    /// 标记支付失败，校验状态合法（待支付或渠道已下单，不可为已支付/已关闭），置失败态并发布 <see cref="PaymentFailedDomainEvent"/>。
    /// </summary>
    /// <param name="reason">失败原因。</param>
    public void MarkFailed(string reason)
    {
        if (Status != PaymentStatus.Pending && Status != PaymentStatus.ChannelOrdered)
        {
            throw new PaymentDomainException(
                $"当前状态 {Status} 不可标记支付失败，仅 Pending/ChannelOrdered 可标记",
                "PAYMENT_FAIL_STATUS_INVALID");
        }

        Status = PaymentStatus.Failed;
        FailReason = reason;
        AddDomainEvent(new PaymentFailedDomainEvent(OrderId, UserId, reason, DateTime.UtcNow));
    }

    /// <summary>
    /// 标记关闭，校验状态合法（待支付、渠道已下单或已失败，不可为已支付），置已关闭态并发布 <see cref="PaymentClosedDomainEvent"/>。
    /// </summary>
    /// <param name="reason">关闭原因。</param>
    public void MarkClosed(string reason)
    {
        if (Status != PaymentStatus.Pending
            && Status != PaymentStatus.ChannelOrdered
            && Status != PaymentStatus.Failed)
        {
            throw new PaymentDomainException(
                $"当前状态 {Status} 不可关闭，仅 Pending/ChannelOrdered/Failed 可关闭",
                "PAYMENT_CLOSE_STATUS_INVALID");
        }

        Status = PaymentStatus.Closed;
        FailReason = reason;
        AddDomainEvent(new PaymentClosedDomainEvent(Id, OrderId, reason, DateTime.UtcNow));
    }
}
