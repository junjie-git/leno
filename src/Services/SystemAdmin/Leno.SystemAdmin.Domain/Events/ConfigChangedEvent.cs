using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;

namespace Leno.SystemAdmin.Domain.Events;

/// <summary>
/// 系统配置变更集成事件，系统管理域在 SystemConfig.Update/Enable/Disable 时发布。
/// 消费方：各业务域（刷新本地配置缓存，保证配置读取及时生效）。
/// 同时实现 <see cref="IDomainEvent"/> 以便经发件箱模式在同一事务内持久化。
/// </summary>
public sealed class ConfigChangedEvent : IntegrationEventBase, IDomainEvent
{
    /// <summary>配置标识。</summary>
    public Guid ConfigId { get; init; }

    /// <summary>配置键。</summary>
    public string ConfigKey { get; init; } = string.Empty;

    /// <summary>配置值。</summary>
    public string ConfigValue { get; init; } = string.Empty;

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => ConfigId;

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public ConfigChangedEvent() : base()
    {
    }

    public ConfigChangedEvent(Guid configId, string configKey, string configValue) : base()
    {
        ConfigId = configId;
        ConfigKey = configKey ?? string.Empty;
        ConfigValue = configValue ?? string.Empty;
    }
}
