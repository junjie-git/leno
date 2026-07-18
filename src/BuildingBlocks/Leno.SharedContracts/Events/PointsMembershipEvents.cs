namespace Leno.SharedContracts.Events;

/// <summary>
/// 积分到账集成事件，积分与会员域在积分入账时发布。
/// 消费方：消息通知域（积分到账通知）。
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class PointsEarnedIntegrationEvent : IntegrationEventBase
{
    /// <summary>积分账户标识。</summary>
    public Guid AccountId { get; init; }

    /// <summary>所属用户标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>入账积分数量。</summary>
    public int Amount { get; init; }

    /// <summary>积分来源（CheckIn/Consumption/Activity/Refund/Offset）。</summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => AccountId;

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public PointsEarnedIntegrationEvent() : base()
    {
    }

    public PointsEarnedIntegrationEvent(Guid accountId, Guid userId, int amount, string source)
        : base()
    {
        AccountId = accountId;
        UserId = userId;
        Amount = amount;
        Source = source ?? string.Empty;
    }
}

/// <summary>
/// 积分消费集成事件，积分与会员域在直接消费积分时发布。
/// 消费方：消息通知域（积分消费通知）、数据分析域。
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class PointsConsumedIntegrationEvent : IntegrationEventBase
{
    public Guid AccountId { get; init; }

    public Guid UserId { get; init; }

    public int Amount { get; init; }

    public Guid ReferenceId { get; init; }

    public string Reason { get; init; } = string.Empty;

    public Guid AggregateId => AccountId;

    public PointsConsumedIntegrationEvent() : base()
    {
    }

    public PointsConsumedIntegrationEvent(Guid accountId, Guid userId, int amount, Guid referenceId, string reason)
        : base()
    {
        AccountId = accountId;
        UserId = userId;
        Amount = amount;
        ReferenceId = referenceId;
        Reason = reason ?? string.Empty;
    }
}

/// <summary>
/// 积分扣回集成事件，积分与会员域在扣回已发放积分时发布。
/// 消费方：消息通知域（积分扣回通知）、数据分析域。
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class PointsRevertedIntegrationEvent : IntegrationEventBase
{
    public Guid AccountId { get; init; }

    public Guid UserId { get; init; }

    public int Amount { get; init; }

    public Guid ReferenceId { get; init; }

    public string Reason { get; init; } = string.Empty;

    public Guid AggregateId => AccountId;

    public PointsRevertedIntegrationEvent() : base()
    {
    }

    public PointsRevertedIntegrationEvent(Guid accountId, Guid userId, int amount, Guid referenceId, string reason)
        : base()
    {
        AccountId = accountId;
        UserId = userId;
        Amount = amount;
        ReferenceId = referenceId;
        Reason = reason ?? string.Empty;
    }
}

/// <summary>
/// 会员等级变更集成事件，积分与会员域在等级发生变化（升级/评估变更）时发布。
/// 消费方：消息通知域（等级变更通知）。
/// 同时覆盖消费门槛升级与成长值评估两种场景。
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class MemberLevelChangedIntegrationEvent : IntegrationEventBase
{
    /// <summary>所属用户标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>变更前等级编号。</summary>
    public int OldLevel { get; init; }

    /// <summary>变更后等级编号。</summary>
    public int NewLevel { get; init; }

    /// <summary>当前成长值（成长值评估场景填充，消费门槛升级场景为 0）。</summary>
    public int GrowthValue { get; init; }

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => UserId;

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public MemberLevelChangedIntegrationEvent() : base()
    {
    }

    public MemberLevelChangedIntegrationEvent(Guid userId, int oldLevel, int newLevel, int growthValue)
        : base()
    {
        UserId = userId;
        OldLevel = oldLevel;
        NewLevel = newLevel;
        GrowthValue = growthValue;
    }
}

/// <summary>
/// 付费会员订阅集成事件，积分与会员域在会员订阅订单支付成功激活 UserMembership 时发布。
/// 消费方：消息通知域（会员开通通知）。
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class PaidMemberSubscribedIntegrationEvent : IntegrationEventBase
{
    public Guid UserId { get; init; }

    public Guid PackageId { get; init; }

    public int Level { get; init; }

    public DateTime EndTime { get; init; }

    public Guid AggregateId => UserId;

    public PaidMemberSubscribedIntegrationEvent() : base()
    {
    }

    public PaidMemberSubscribedIntegrationEvent(Guid userId, Guid packageId, int level, DateTime endTime)
        : base()
    {
        UserId = userId;
        PackageId = packageId;
        Level = level;
        EndTime = endTime;
    }
}
