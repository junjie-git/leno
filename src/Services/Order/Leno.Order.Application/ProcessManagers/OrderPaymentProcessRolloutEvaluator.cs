namespace Leno.Order.Application.ProcessManagers;

/// <summary>
/// 支付流程编排（Process Manager）灰度切流评估器。
/// 根据全局开关 <see cref="OrderPaymentProcessOptions.UsePaymentProcessManager"/> 与
/// 灰度百分比 <see cref="OrderPaymentProcessOptions.RolloutPercent"/> 判断指定订单是否走 Process Manager 跟踪路径。
/// 哈希键使用 <c>OrderId</c>（与 Saga 状态机使用 UserId 不同），保证同一订单切流稳定。
/// </summary>
public static class OrderPaymentProcessRolloutEvaluator
{
    /// <summary>
    /// 判断指定订单是否应走 Process Manager 跟踪路径。
    /// </summary>
    /// <param name="options">Process Manager 配置选项。</param>
    /// <param name="orderId">订单标识，用于灰度哈希。</param>
    /// <returns>
    /// true：消费者在完成子任务后将完成回调转发给 <see cref="IOrderPaymentProcessManager"/>；
    /// false：仅走旧路径，不创建 Process Manager 状态、不发布编排事件。
    /// </returns>
    public static bool ShouldUseProcessManager(OrderPaymentProcessOptions options, Guid orderId)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!options.UsePaymentProcessManager)
        {
            return false;
        }

        var rollout = options.RolloutPercent;
        if (rollout >= 100)
        {
            return true;
        }
        if (rollout <= 0)
        {
            return false;
        }

        // 灰度哈希：OrderId 的哈希值取模 100，保证同一订单切流稳定
        var hash = unchecked((uint)orderId.GetHashCode());
        return hash % 100 < (uint)rollout;
    }
}
