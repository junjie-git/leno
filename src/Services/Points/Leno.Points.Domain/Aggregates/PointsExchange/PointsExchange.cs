using Leno.Points.Domain.Events;
using Leno.Points.Domain.Exceptions;
using Leno.Points.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.Points.Domain.Aggregates.PointsExchange;

/// <summary>
/// 积分兑换状态枚举。
/// 流转：Pending → Completed（兑换成功）/ Failed（兑换失败）/ Cancelled（取消）。
/// </summary>
public enum ExchangeStatus
{
    /// <summary>待处理：已扣减积分，等待外部系统（优惠券域）确认。</summary>
    Pending = 0,

    /// <summary>已完成：外部系统确认兑换成功。</summary>
    Completed = 1,

    /// <summary>已失败：外部系统确认兑换失败，积分已回补。</summary>
    Failed = 2,

    /// <summary>已取消：业务方主动取消，积分已回补。</summary>
    Cancelled = 3
}

/// <summary>
/// 积分兑换聚合根，封装用户使用积分兑换商品/优惠券的完整生命周期。
/// 聚合标识 <see cref="Entity.Id"/> 即对外 <c>ExchangeId</c>。
/// 不变量：状态机严格按 Pending → Completed/Failed/Cancelled 流转，积分扣减与状态变更原子化。
/// </summary>
public sealed class PointsExchange : AggregateRoot
{
    /// <summary>兑换发起用户标识。</summary>
    public Guid UserId { get; private set; }

    /// <summary>兑换目标标识（优惠券模板 ID / 商品 SKU ID 等）。</summary>
    public Guid TargetId { get; private set; }

    /// <summary>兑换类型（CouponExchange / GoodsExchange）。</summary>
    public ExchangeType Type { get; private set; }

    /// <summary>消耗积分数量。</summary>
    public int PointsRequired { get; private set; }

    /// <summary>关联的积分账户标识（用于回补积分时定位账户）。</summary>
    public Guid PointsAccountId { get; private set; }

    /// <summary>兑换状态。</summary>
    public ExchangeStatus Status { get; private set; }

    /// <summary>兑换发起时间（UTC）。</summary>
    public DateTime RequestedAt { get; private set; }

    /// <summary>兑换完成/失败/取消时间（UTC）。</summary>
    public DateTime? CompletedAt { get; private set; }

    /// <summary>失败/取消原因。</summary>
    public string? Reason { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private PointsExchange() { }

    private PointsExchange(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，校验参数合法性，初始状态为 Pending。
    /// </summary>
    /// <param name="exchangeId">兑换标识，由应用层生成。</param>
    /// <param name="userId">发起用户标识。</param>
    /// <param name="pointsAccountId">积分账户标识。</param>
    /// <param name="targetId">兑换目标标识。</param>
    /// <param name="type">兑换类型。</param>
    /// <param name="pointsRequired">消耗积分数量，须 &gt; 0。</param>
    public static PointsExchange Create(
        Guid exchangeId,
        Guid userId,
        Guid pointsAccountId,
        Guid targetId,
        ExchangeType type,
        int pointsRequired)
    {
        if (userId == Guid.Empty)
        {
            throw new PointsDomainException("UserId 不可为空", "POINTS_USER_EMPTY");
        }

        if (pointsAccountId == Guid.Empty)
        {
            throw new PointsDomainException("PointsAccountId 不可为空", "POINTS_ACCOUNT_EMPTY");
        }

        if (targetId == Guid.Empty)
        {
            throw new PointsDomainException("TargetId 不可为空", "POINTS_EXCHANGE_TARGET_EMPTY");
        }

        if (pointsRequired <= 0)
        {
            throw new PointsDomainException("兑换积分数量须大于 0", "POINTS_EXCHANGE_AMOUNT_INVALID");
        }

        return new PointsExchange(exchangeId == Guid.Empty ? Guid.NewGuid() : exchangeId)
        {
            UserId = userId,
            PointsAccountId = pointsAccountId,
            TargetId = targetId,
            Type = type,
            PointsRequired = pointsRequired,
            Status = ExchangeStatus.Pending,
            RequestedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// 标记兑换完成，仅 Pending 态可完成，发布 <see cref="PointsExchangeCompletedDomainEvent"/>。
    /// </summary>
    public void Complete()
    {
        if (Status != ExchangeStatus.Pending)
        {
            throw new PointsDomainException(
                $"当前状态 {Status} 不可完成，仅 Pending 可完成",
                "POINTS_EXCHANGE_COMPLETE_INVALID");
        }

        Status = ExchangeStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        AddDomainEvent(new PointsExchangeCompletedDomainEvent(Id, UserId, TargetId, PointsRequired, Type));
    }

    /// <summary>
    /// 标记兑换失败，仅 Pending 态可失败，发布 <see cref="PointsExchangeFailedDomainEvent"/>。
    /// 调用方应在消费此事件后回补积分账户余额。
    /// </summary>
    /// <param name="reason">失败原因。</param>
    public void Fail(string reason)
    {
        if (Status != ExchangeStatus.Pending)
        {
            throw new PointsDomainException(
                $"当前状态 {Status} 不可失败，仅 Pending 可失败",
                "POINTS_EXCHANGE_FAIL_INVALID");
        }

        Status = ExchangeStatus.Failed;
        CompletedAt = DateTime.UtcNow;
        Reason = reason ?? string.Empty;
        AddDomainEvent(new PointsExchangeFailedDomainEvent(Id, UserId, PointsAccountId, PointsRequired, Reason));
    }

    /// <summary>
    /// 取消兑换，仅 Pending 态可取消，发布 <see cref="PointsExchangeFailedDomainEvent"/> 触发积分回补。
    /// </summary>
    /// <param name="reason">取消原因。</param>
    public void Cancel(string reason)
    {
        if (Status != ExchangeStatus.Pending)
        {
            throw new PointsDomainException(
                $"当前状态 {Status} 不可取消，仅 Pending 可取消",
                "POINTS_EXCHANGE_CANCEL_INVALID");
        }

        Status = ExchangeStatus.Cancelled;
        CompletedAt = DateTime.UtcNow;
        Reason = reason ?? string.Empty;
        AddDomainEvent(new PointsExchangeFailedDomainEvent(Id, UserId, PointsAccountId, PointsRequired, Reason));
    }
}

/// <summary>
/// 积分兑换类型枚举。
/// </summary>
public enum ExchangeType
{
    /// <summary>兑换优惠券。</summary>
    CouponExchange = 0,

    /// <summary>兑换实物商品。</summary>
    GoodsExchange = 1
}
