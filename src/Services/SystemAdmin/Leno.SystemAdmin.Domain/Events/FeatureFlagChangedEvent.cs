using Leno.SharedKernel.Abstractions;

namespace Leno.SystemAdmin.Domain.Events;

/// <summary>
/// 特性开关变更领域事件，系统管理域在 FeatureFlag.Enable/Disable/Update 时由聚合根收集。
/// mapper 翻译为 <see cref="Leno.SharedContracts.Events.FeatureFlagChangedIntegrationEvent"/> 集成事件对外发布。
/// </summary>
public sealed class FeatureFlagChangedEvent : DomainEventBase
{
    /// <summary>特性开关标识。</summary>
    public Guid FlagId { get; init; }

    /// <summary>开关键。</summary>
    public string FlagKey { get; init; } = string.Empty;

    /// <summary>是否启用。</summary>
    public bool IsEnabled { get; init; }

    /// <summary>评估策略（0=全局，1=用户白名单，2=按角色，3=按比例），以 int 传递避免跨域枚举依赖。</summary>
    public int Strategy { get; init; }

    public FeatureFlagChangedEvent(Guid flagId, string flagKey, bool isEnabled, int strategy)
        : base(flagId)
    {
        FlagId = flagId;
        FlagKey = flagKey ?? string.Empty;
        IsEnabled = isEnabled;
        Strategy = strategy;
    }
}
