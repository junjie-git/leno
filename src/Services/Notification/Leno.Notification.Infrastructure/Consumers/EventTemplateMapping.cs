namespace Leno.Notification.Infrastructure.Consumers;

/// <summary>
/// 事件类型到通知模板编码的映射，作为事件消费者与通知模板之间的单一事实来源。
/// 所有需要从集成事件类型推导模板编码的消费者均通过此映射获取。
/// </summary>
public static class EventTemplateMapping
{
    public static readonly Dictionary<string, string> Mappings = new()
    {
        ["UserRegisteredEvent"] = "user_registered_welcome",
        ["OrderCreatedEvent"] = "order_created",
        ["OrderShippedEvent"] = "order_shipped",
        ["OrderCompletedEvent"] = "order_completed",
        ["PaymentSucceededEvent"] = "payment_succeeded",
        ["PaymentFailedEvent"] = "payment_failed",
        ["AfterSalesApprovedEvent"] = "after_sales_approved",
        ["RefundCompletedEvent"] = "refund_completed",
        ["SeckillOrderCreatedEvent"] = "seckill_order_created",
        ["PointsEarnedEvent"] = "points_earned",
        ["MemberLevelUpgradedEvent"] = "member_level_upgraded",
        ["MembershipActivatedEvent"] = "membership_activated",
    };

    /// <summary>
    /// 根据事件类型名称获取对应的模板编码，未映射时返回 null。
    /// </summary>
    public static string? GetTemplateCode(string eventType) =>
        eventType is not null && Mappings.TryGetValue(eventType, out var code) ? code : null;
}