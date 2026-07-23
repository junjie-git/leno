namespace Leno.Promotion.Domain.Rules;

/// <summary>
/// 规则叠加策略，由 <see cref="IRuleEngine"/> 编排时使用。
/// </summary>
public enum StackingPolicy
{
    /// <summary>
    /// 互斥：一旦本规则被应用，立即中断后续规则评估，后续规则不再执行。
    /// 用于强互斥场景（如秒杀价不与满减叠加）。
    /// </summary>
    Exclusive = 0,

    /// <summary>
    /// 可叠加：本规则折扣金额从剩余订单金额中扣减后，继续评估后续规则。
    /// 用于可叠加场景（如满减 + 优惠券）。
    /// </summary>
    Stackable = 1,

    /// <summary>
    /// 最优选择：对同一上下文评估所有同组 Stackable 规则后取折扣最大者，
    /// 仅保留最优规则结果，其余丢弃。用于"满减/优惠券二选一"等择优场景。
    /// </summary>
    BestOf = 2
}
