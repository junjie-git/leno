using Leno.SharedKernel.Abstractions;

namespace Leno.SystemAdmin.Domain.Events;

/// <summary>
/// 系统配置变更领域事件，系统管理域在 SystemConfig.Update/Enable/Disable 时由聚合根收集。
/// mapper 翻译为 <see cref="Leno.SharedContracts.Events.ConfigChangedIntegrationEvent"/> 集成事件对外发布。
/// </summary>
public sealed class ConfigChangedEvent : DomainEventBase
{
    /// <summary>配置标识。</summary>
    public Guid ConfigId { get; init; }

    /// <summary>配置键。</summary>
    public string ConfigKey { get; init; } = string.Empty;

    /// <summary>配置值。</summary>
    public string ConfigValue { get; init; } = string.Empty;

    public ConfigChangedEvent(Guid configId, string configKey, string configValue)
        : base(configId)
    {
        ConfigId = configId;
        ConfigKey = configKey ?? string.Empty;
        ConfigValue = configValue ?? string.Empty;
    }
}
