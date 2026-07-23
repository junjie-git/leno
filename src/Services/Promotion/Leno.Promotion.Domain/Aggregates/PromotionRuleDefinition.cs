using Leno.Promotion.Domain.Events;
using Leno.Promotion.Domain.Exceptions;
using Leno.Promotion.Domain.Rules;
using Leno.SharedKernel.Abstractions;

namespace Leno.Promotion.Domain.Aggregates;

/// <summary>
/// 促销规则定义聚合根，持久化"规则类型 + 优先级 + 叠加策略 + JSON 规则体"四要素。
/// 规则定义独立于具体规则实现：<see cref="RuleType"/> 对应 <see cref="IPromotionRule.RuleType"/>，
/// <see cref="DefinitionJson"/> 持有规则体 JSON（如满减档位、优惠券门槛），由 <c>JsonRuleLoader</c> 加载供规则实现读取。
/// 修改时发布 <see cref="PromotionRuleDefinitionChangedEvent"/> 触发热刷新。
/// 聚合标识 <see cref="Entity.Id"/> 即对外 <c>RuleDefinitionId</c>。
/// </summary>
public sealed class PromotionRuleDefinition : AggregateRoot
{
    /// <summary>
    /// 规则类型标识（如 "FullReduction" / "Coupon" / "SeckillDiscount"）。
    /// 与 <see cref="IPromotionRule.RuleType"/> 对齐，便于 <c>JsonRuleLoader</c> 匹配实现类。
    /// </summary>
    public string RuleType { get; private set; } = string.Empty;

    /// <summary>规则显示名，便于运营与诊断。</summary>
    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>规则优先级，数字越小越先评估。与 <see cref="IPromotionRule.Priority"/> 含义一致。</summary>
    public int Priority { get; private set; }

    /// <summary>规则叠加策略。</summary>
    public StackingPolicy Stacking { get; private set; }

    /// <summary>
    /// 规则体 JSON，承载满减档位、优惠券门槛、秒杀活动绑定等具体配置。
    /// 由 <c>JsonRuleLoader</c> 反序列化为 <c>JsonRuleDefinition</c>，规则实现按 RuleType 取对应字段。
    /// </summary>
    public string DefinitionJson { get; private set; } = "{}";

    /// <summary>规则是否启用。禁用规则不参与 <see cref="IRuleEngine"/> 编排。</summary>
    public bool Enabled { get; private set; }

    /// <summary>
    /// 规则定义版本号（运营自定义，如 "2026.07.01"），用于热刷新时变更检测。
    /// 同 <see cref="RuleType"/> 的定义修改后 <see cref="DefinitionVersion"/> 必须变更，触发 <c>JsonRuleLoader</c> 重新加载。
    /// 注意：命名为 <c>DefinitionVersion</c> 而非 <c>Version</c>，避免与 <see cref="BaseDbContext"/> 自动注入的
    /// <c>Version</c> rowversion shadow property 名冲突。
    /// </summary>
    public string DefinitionVersion { get; private set; } = string.Empty;

    /// <summary>备注说明，便于运营协作。</summary>
    public string? Remark { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private PromotionRuleDefinition() { }

    private PromotionRuleDefinition(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，创建规则定义，初始状态为启用。
    /// </summary>
    /// <param name="ruleDefinitionId">规则定义标识，由应用层生成。</param>
    /// <param name="ruleType">规则类型标识（与 <see cref="IPromotionRule.RuleType"/> 对齐）。</param>
    /// <param name="displayName">规则显示名。</param>
    /// <param name="priority">规则优先级，须 ≥ 0。</param>
    /// <param name="stacking">叠加策略。</param>
    /// <param name="definitionJson">规则体 JSON，不可为空。</param>
    /// <param name="version">版本号，不可为空。</param>
    /// <param name="remark">备注，可空。</param>
    public static PromotionRuleDefinition Create(
        Guid ruleDefinitionId,
        string ruleType,
        string displayName,
        int priority,
        StackingPolicy stacking,
        string definitionJson,
        string version,
        string? remark = null)
    {
        ValidateCommon(ruleType, displayName, definitionJson, version);

        if (priority < 0)
        {
            throw new PromotionDomainException("规则优先级不可为负", "RULE_PRIORITY_INVALID");
        }

        return new PromotionRuleDefinition(ruleDefinitionId == Guid.Empty ? Guid.NewGuid() : ruleDefinitionId)
        {
            RuleType = ruleType,
            DisplayName = displayName,
            Priority = priority,
            Stacking = stacking,
            DefinitionJson = definitionJson,
            Enabled = true,
            DefinitionVersion = version,
            Remark = remark
        };
    }

    /// <summary>
    /// 更新规则定义，触发 <see cref="PromotionRuleDefinitionChangedEvent"/> 通知热刷新。
    /// <see cref="RuleType"/> 不可修改（避免破坏 <c>JsonRuleLoader</c> 的 RuleType 映射），仅可修改规则体与策略。
    /// </summary>
    public void Update(
        string displayName,
        int priority,
        StackingPolicy stacking,
        string definitionJson,
        string version,
        string? remark = null)
    {
        ValidateCommon(RuleType, displayName, definitionJson, version);

        if (priority < 0)
        {
            throw new PromotionDomainException("规则优先级不可为负", "RULE_PRIORITY_INVALID");
        }

        var changed = !string.Equals(DisplayName, displayName, StringComparison.Ordinal)
            || Priority != priority
            || Stacking != stacking
            || !string.Equals(DefinitionJson, definitionJson, StringComparison.Ordinal)
            || !string.Equals(DefinitionVersion, version, StringComparison.Ordinal);

        DisplayName = displayName;
        Priority = priority;
        Stacking = stacking;
        DefinitionJson = definitionJson;
        DefinitionVersion = version;
        Remark = remark;

        if (changed)
        {
            AddDomainEvent(new PromotionRuleDefinitionChangedEvent(Id, RuleType, DefinitionVersion));
        }
    }

    /// <summary>启用规则定义，发布变更事件触发 <c>JsonRuleLoader</c> 重新加载。</summary>
    public void Enable()
    {
        if (Enabled)
        {
            throw new PromotionDomainException("规则已启用", "RULE_ALREADY_ENABLED");
        }

        Enabled = true;
        AddDomainEvent(new PromotionRuleDefinitionChangedEvent(Id, RuleType, DefinitionVersion));
    }

    /// <summary>禁用规则定义，发布变更事件触发 <c>JsonRuleLoader</c> 重新加载。</summary>
    public void Disable()
    {
        if (!Enabled)
        {
            throw new PromotionDomainException("规则已禁用", "RULE_ALREADY_DISABLED");
        }

        Enabled = false;
        AddDomainEvent(new PromotionRuleDefinitionChangedEvent(Id, RuleType, DefinitionVersion));
    }

    private static void ValidateCommon(string ruleType, string displayName, string definitionJson, string version)
    {
        if (string.IsNullOrWhiteSpace(ruleType))
        {
            throw new PromotionDomainException("规则类型不可为空", "RULE_TYPE_EMPTY");
        }

        if (ruleType.Length > 64)
        {
            throw new PromotionDomainException("规则类型长度不可超过 64", "RULE_TYPE_TOO_LONG");
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new PromotionDomainException("规则显示名不可为空", "RULE_NAME_EMPTY");
        }

        if (string.IsNullOrWhiteSpace(definitionJson))
        {
            throw new PromotionDomainException("规则体 JSON 不可为空", "RULE_JSON_EMPTY");
        }

        if (string.IsNullOrWhiteSpace(version))
        {
            throw new PromotionDomainException("规则版本号不可为空", "RULE_VERSION_EMPTY");
        }

        if (version.Length > 32)
        {
            throw new PromotionDomainException("规则版本号长度不可超过 32", "RULE_VERSION_TOO_LONG");
        }
    }
}
