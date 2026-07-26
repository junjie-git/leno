using Leno.PointsMembership.Domain.Exceptions;
using Leno.PointsMembership.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.PointsMembership.Domain.Aggregates;

/// <summary>
/// 积分规则聚合根，运营配置的积分发放规则，封装编码、行为类型、积分值、每日上限与状态的不变量。
/// 聚合标识 <see cref="Entity.Id"/> 即对外 <c>RuleId</c>。
/// 规则编码（Code）全局唯一，创建后不可修改；积分值支持正负（正数发放、负数扣减）。
/// </summary>
public sealed class PointsRule : AggregateRoot
{
    /// <summary>积分值常量下限（含扣减场景），对应 design-prompts 校验规则 -1000。</summary>
    public const int PointsMinValue = -1000;

    /// <summary>积分值常量上限，对应 design-prompts 校验规则 1000。</summary>
    public const int PointsMaxValue = 1000;

    /// <summary>每日上限常量下限，对应 design-prompts 校验规则 1。</summary>
    public const int DailyLimitMinValue = 1;

    /// <summary>每日上限常量上限，对应 design-prompts 校验规则 100。</summary>
    public const int DailyLimitMaxValue = 100;

    /// <summary>规则编码，全局唯一，创建后不可修改。如 DAILY_CHECK、ORDER_DONE。</summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>规则名称，如"每日签到"、"下单得积分"。</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>行为类型，标识规则对应的用户行为类别。</summary>
    public PointsActionType ActionType { get; private set; }

    /// <summary>积分值，正数为发放、负数为扣减，范围 [-1000, 1000]。</summary>
    public int Points { get; private set; }

    /// <summary>每日上限，单用户每日通过此规则可获取的次数上限，范围 [1, 100]。</summary>
    public int DailyLimit { get; private set; }

    /// <summary>规则状态，Enabled 生效参与积分发放， Disabled 不参与。</summary>
    public PointsRuleStatus Status { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private PointsRule() { }

    private PointsRule(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，校验编码、名称、积分值、每日上限合法性，初始状态由 <paramref name="status"/> 指定（默认 Enabled）。
    /// </summary>
    /// <param name="ruleId">规则标识，由应用层生成。</param>
    /// <param name="code">规则编码，全局唯一。</param>
    /// <param name="name">规则名称。</param>
    /// <param name="actionType">行为类型。</param>
    /// <param name="points">积分值，支持正负。</param>
    /// <param name="dailyLimit">每日上限，1-100。</param>
    /// <param name="status">初始状态，默认 Enabled。</param>
    public static PointsRule Create(
        Guid ruleId,
        string code,
        string name,
        PointsActionType actionType,
        int points,
        int dailyLimit,
        PointsRuleStatus status = PointsRuleStatus.Enabled)
    {
        Validate(code, name, points, dailyLimit);

        return new PointsRule(ruleId == Guid.Empty ? Guid.NewGuid() : ruleId)
        {
            Code = code,
            Name = name,
            ActionType = actionType,
            Points = points,
            DailyLimit = dailyLimit,
            Status = status
        };
    }

    /// <summary>
    /// 更新规则可编辑字段（编码不可改，状态经启用/停用端点切换）。
    /// </summary>
    /// <param name="name">规则名称。</param>
    /// <param name="actionType">行为类型。</param>
    /// <param name="points">积分值，支持正负。</param>
    /// <param name="dailyLimit">每日上限，1-100。</param>
    public void Update(
        string name,
        PointsActionType actionType,
        int points,
        int dailyLimit)
    {
        Validate(Code, name, points, dailyLimit);

        Name = name;
        ActionType = actionType;
        Points = points;
        DailyLimit = dailyLimit;
    }

    /// <summary>启用规则，已启用时抛出 <see cref="PointsDomainException"/>。</summary>
    public void Enable()
    {
        if (Status == PointsRuleStatus.Enabled)
        {
            throw new PointsDomainException("积分规则已启用", "POINTS_RULE_ALREADY_ENABLED");
        }

        Status = PointsRuleStatus.Enabled;
    }

    /// <summary>停用规则，已停用时抛出 <see cref="PointsDomainException"/>。</summary>
    public void Disable()
    {
        if (Status == PointsRuleStatus.Disabled)
        {
            throw new PointsDomainException("积分规则已停用", "POINTS_RULE_ALREADY_DISABLED");
        }

        Status = PointsRuleStatus.Disabled;
    }

    private static void Validate(string code, string name, int points, int dailyLimit)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new PointsDomainException("积分规则编码不可为空", "POINTS_RULE_CODE_EMPTY");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new PointsDomainException("积分规则名称不可为空", "POINTS_RULE_NAME_EMPTY");
        }

        if (points < PointsMinValue || points > PointsMaxValue)
        {
            throw new PointsDomainException(
                $"积分值须在 {PointsMinValue} 到 {PointsMaxValue} 之间（支持扣减）",
                "POINTS_RULE_POINTS_INVALID");
        }

        if (dailyLimit < DailyLimitMinValue || dailyLimit > DailyLimitMaxValue)
        {
            throw new PointsDomainException(
                $"每日上限须为 {DailyLimitMinValue} 到 {DailyLimitMaxValue} 之间的正整数",
                "POINTS_RULE_DAILY_LIMIT_INVALID");
        }
    }
}
