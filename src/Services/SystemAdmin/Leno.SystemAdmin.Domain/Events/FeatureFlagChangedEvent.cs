using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;

namespace Leno.SystemAdmin.Domain.Events;

/// <summary>
/// 特性开关变更集成事件，系统管理域在 FeatureFlag.Enable/Disable/Update 时发布。
/// 消费方：各业务域（刷新本地特性开关缓存，保证评估结果及时生效）。
/// 同时实现 <see cref="IDomainEvent"/> 以便经发件箱模式在同一事务内持久化。
/// </summary>
public sealed class FeatureFlagChangedEvent : IntegrationEventBase, IDomainEvent
{
    /// <summary>特性开关标识。</summary>
    public Guid FlagId { get; init; }

    /// <summary>开关键。</summary>
    public string FlagKey { get; init; } = string.Empty;

    /// <summary>是否启用。</summary>
    public bool IsEnabled { get; init; }

    /// <summary>评估策略（0=全局，1=用户白名单，2=按角色，3=按比例），以 int 传递避免跨域枚举依赖。</summary>
    public int Strategy { get; init; }

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => FlagId;

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public FeatureFlagChangedEvent() : base()
    {
    }

    public FeatureFlagChangedEvent(Guid flagId, string flagKey, bool isEnabled, int strategy) : base()
    {
        FlagId = flagId;
        FlagKey = flagKey ?? string.Empty;
        IsEnabled = isEnabled;
        Strategy = strategy;
    }
}
