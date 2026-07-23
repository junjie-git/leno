namespace Leno.Order.Application.ProcessManagers;

/// <summary>
/// 订单支付流程编排（Process Manager）配置选项，支持双轨期 feature flag 切流。
/// 配置路径：<c>Order:UsePaymentProcessManager</c>，默认 false（走旧路径：直接消费 PaymentSucceededEvent 执行三子任务）。
/// 切流策略：按 OrderId 哈希百分比（<see cref="RolloutPercent"/>）灰度切流，
/// 由 <c>PaymentSucceededEventConsumer</c> / <c>StockConfirmConsumer</c> / <c>PointsConfirmConsumer</c>
/// 在消费时判断是否将子任务完成回调转发给 <see cref="OrderPaymentProcessManager"/>。
/// shadow 模式：flag=true 时旧路径（直接执行子任务）仍运行以保证功能兼容，
/// Process Manager 在其之上跟踪三个子任务完成度并发布编排事件，供未来全量切流。
/// </summary>
public sealed class OrderPaymentProcessOptions
{
    /// <summary>
    /// 是否启用支付流程编排（Process Manager）跟踪路径。
    /// true：三个消费者在完成子任务后将完成回调转发给 <see cref="OrderPaymentProcessManager"/>，
    ///       Process Manager 创建状态记录、跟踪完成度、发布编排事件（shadow 模式：旧路径仍执行实际工作）。
    /// false：仅走旧路径，不创建 Process Manager 状态、不发布编排事件。
    /// </summary>
    public bool UsePaymentProcessManager { get; init; }

    /// <summary>
    /// 切流百分比（0-100），用于按 OrderId 哈希灰度切流。
    /// 100 表示全部走 Process Manager 跟踪路径，0 表示全部走旧路径。
    /// 与 <see cref="UsePaymentProcessManager"/> 配合：全局开关为 true 时按本字段灰度。
    /// 保证同一订单切流稳定（不会时而跟踪时而不跟踪）。
    /// </summary>
    public int RolloutPercent { get; init; } = 0;
}
