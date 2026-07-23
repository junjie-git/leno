namespace Leno.Order.Application.Sagas;

/// <summary>
/// Order Saga 状态机配置选项，支持双轨期 feature flag 切流。
/// 配置路径：<c>Order:UseSagaStateMachine</c>，默认 false（走旧进程内编排路径）。
/// 切流策略：按 OrderId 哈希百分比（10% → 50% → 100%），由 <see cref="OrderSagaOrchestrator"/> 在 ExecuteAsync 中判断。
/// </summary>
public sealed class OrderSagaOptions
{
    /// <summary>
    /// 是否启用 Saga 状态机编排路径。
    /// true：OrderSagaOrchestrator 发布 <see cref="Events.OrderSagaStarted"/> 事件启动 Saga（shadow 模式：旧进程内编排仍执行以保证返回值兼容）。
    /// false：仅走旧进程内编排路径，不发布 Saga 事件。
    /// </summary>
    public bool UseSagaStateMachine { get; init; }

    /// <summary>
    /// Saga 切流百分比（0-100），用于按 OrderId 哈希灰度切流。
    /// 100 表示全部走 Saga 路径，0 表示全部走旧路径。
    /// 与 <see cref="UseSagaStateMachine"/> 配合：<see cref="UseSagaStateMachine"/>=true 且本字段=100 时全量切流。
    /// </summary>
    public int RolloutPercent { get; init; } = 0;
}
