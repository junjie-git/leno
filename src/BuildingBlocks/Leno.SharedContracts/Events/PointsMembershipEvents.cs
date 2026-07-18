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

/// <summary>
/// 积分账户创建集成事件，积分与会员域在新建积分账户时发布。
/// 消费方：积分与会员域读模型同步（索引到 ES leno_points_accounts）。
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class PointsAccountCreatedEvent : IntegrationEventBase
{
    /// <summary>积分账户标识。</summary>
    public Guid PointsAccountId { get; init; }

    /// <summary>所属用户标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>初始积分余额（新建账户一般为 0；含新人积分时为发放数额）。</summary>
    public int InitialPoints { get; init; }

    /// <summary>账户创建时间（UTC）。</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => PointsAccountId;

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public PointsAccountCreatedEvent() : base()
    {
    }

    public PointsAccountCreatedEvent(Guid pointsAccountId, Guid userId, int initialPoints, DateTime createdAt)
        : base()
    {
        PointsAccountId = pointsAccountId;
        UserId = userId;
        InitialPoints = initialPoints;
        CreatedAt = createdAt;
    }
}

/// <summary>
/// 积分账户余额变更集成事件，积分与会员域在账户余额发生变化（入账/消费/扣回/冻结/释放/过期）时发布。
/// 消费方：积分与会员域读模型同步（按最新聚合根重建 ES leno_points_accounts 文档，触发 IndexAsync 而非删除）。
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class PointsAdjustedEvent : IntegrationEventBase
{
    /// <summary>积分账户标识。</summary>
    public Guid PointsAccountId { get; init; }

    /// <summary>本次变更数量（正为入账/释放，负为消费/扣回/冻结/过期）。</summary>
    public int Delta { get; init; }

    /// <summary>变更原因。</summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>变更发生时间（UTC）。</summary>
    public DateTime AdjustedAt { get; init; }

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => PointsAccountId;

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public PointsAdjustedEvent() : base()
    {
    }

    public PointsAdjustedEvent(Guid pointsAccountId, int delta, string reason, DateTime adjustedAt)
        : base()
    {
        PointsAccountId = pointsAccountId;
        Delta = delta;
        Reason = reason ?? string.Empty;
        AdjustedAt = adjustedAt;
    }
}

/// <summary>
/// 会员档案创建集成事件，积分与会员域在新建会员档案时发布。
/// 消费方：积分与会员域读模型同步（索引到 ES leno_members）。
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class MemberRegisteredEvent : IntegrationEventBase
{
    /// <summary>会员标识。</summary>
    public Guid MemberId { get; init; }

    /// <summary>所属用户标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>初始会员等级编号。</summary>
    public int Level { get; init; }

    /// <summary>会员档案创建时间（UTC）。</summary>
    public DateTime RegisteredAt { get; init; }

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => MemberId;

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public MemberRegisteredEvent() : base()
    {
    }

    public MemberRegisteredEvent(Guid memberId, Guid userId, int level, DateTime registeredAt)
        : base()
    {
        MemberId = memberId;
        UserId = userId;
        Level = level;
        RegisteredAt = registeredAt;
    }
}

/// <summary>
/// 会员等级升级集成事件（积分与会员域读模型同步专用）。
/// 与 Leno.PointsMembership.Domain.Events.MemberLevelUpgradedEvent 领域事件区分：本事件为跨上下文集成事件，
/// 字段以 MemberId 为聚合标识，由积分与会员域在会员等级升级时发布。
/// 消费方：积分与会员域读模型同步（按最新聚合根重建 ES leno_members 文档，触发 IndexAsync 而非删除）。
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class MemberLevelUpgradedEvent : IntegrationEventBase
{
    /// <summary>会员标识。</summary>
    public Guid MemberId { get; init; }

    /// <summary>升级后等级编号。</summary>
    public int NewLevel { get; init; }

    /// <summary>升级时间（UTC）。</summary>
    public DateTime UpgradedAt { get; init; }

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => MemberId;

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public MemberLevelUpgradedEvent() : base()
    {
    }

    public MemberLevelUpgradedEvent(Guid memberId, int newLevel, DateTime upgradedAt)
        : base()
    {
        MemberId = memberId;
        NewLevel = newLevel;
        UpgradedAt = upgradedAt;
    }
}
