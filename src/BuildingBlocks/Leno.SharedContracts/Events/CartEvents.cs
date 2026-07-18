namespace Leno.SharedContracts.Events;

/// <summary>
/// 购物车合并集成事件，购物车域在登录时合并匿名购物车后发布。
/// 消费方：数据分析域（用户行为追踪）、消息通知域（可选）。
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class CartMergedEvent : IntegrationEventBase
{
    /// <summary>买家账号标识（用户域 UserId）。</summary>
    public Guid UserId { get; init; }

    /// <summary>匿名会话标识（合并前匿名购物车的 SessionId）。</summary>
    public string AnonymousId { get; init; } = string.Empty;

    /// <summary>合并的购物车项数量。</summary>
    public int MergedItemCount { get; init; }

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => UserId;

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public CartMergedEvent() : base()
    {
    }

    public CartMergedEvent(Guid userId, string anonymousId, int mergedItemCount) : base()
    {
        UserId = userId;
        AnonymousId = anonymousId ?? string.Empty;
        MergedItemCount = mergedItemCount;
    }
}