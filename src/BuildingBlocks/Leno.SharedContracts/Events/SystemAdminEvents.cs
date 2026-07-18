namespace Leno.SharedContracts.Events;

/// <summary>
/// 特性开关变更集成事件，系统管理域在 FeatureFlag.Enable/Disable/Update 时发布。
/// 消费方：各业务域（刷新本地特性开关缓存，保证评估结果及时生效）。
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class FeatureFlagChangedIntegrationEvent : IntegrationEventBase
{
    /// <summary>特性开关标识。</summary>
    public Guid FlagId { get; init; }

    /// <summary>开关键。</summary>
    public string FlagKey { get; init; } = string.Empty;

    /// <summary>是否启用。</summary>
    public bool IsEnabled { get; init; }

    /// <summary>评估策略（0=全局，1=用户白名单，2=按角色，3=按比例），以 int 传递避免跨域枚举依赖。</summary>
    public int Strategy { get; init; }

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public FeatureFlagChangedIntegrationEvent() : base() { }

    public FeatureFlagChangedIntegrationEvent(Guid flagId, string flagKey, bool isEnabled, int strategy) : base()
    {
        FlagId = flagId;
        FlagKey = flagKey ?? string.Empty;
        IsEnabled = isEnabled;
        Strategy = strategy;
    }
}

/// <summary>
/// 系统配置变更集成事件，系统管理域在 SystemConfig.Update/Enable/Disable 时发布。
/// 消费方：各业务域（刷新本地配置缓存，保证配置读取及时生效）。
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class ConfigChangedIntegrationEvent : IntegrationEventBase
{
    /// <summary>配置标识。</summary>
    public Guid ConfigId { get; init; }

    /// <summary>配置键。</summary>
    public string ConfigKey { get; init; } = string.Empty;

    /// <summary>配置值。</summary>
    public string ConfigValue { get; init; } = string.Empty;

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public ConfigChangedIntegrationEvent() : base() { }

    public ConfigChangedIntegrationEvent(Guid configId, string configKey, string configValue) : base()
    {
        ConfigId = configId;
        ConfigKey = configKey ?? string.Empty;
        ConfigValue = configValue ?? string.Empty;
    }
}

/// <summary>
/// 公告发布集成事件，系统管理域在 SystemAnnouncement.Publish 时发布。
/// 消费方：消息通知域（向目标受众推送公告通知）。
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class AnnouncementPublishedIntegrationEvent : IntegrationEventBase
{
    /// <summary>公告标识。</summary>
    public Guid AnnouncementId { get; init; }

    /// <summary>公告标题。</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>公告类型（0=系统，1=维护，2=促销），以 int 传递避免跨域枚举依赖。</summary>
    public int Type { get; init; }

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public AnnouncementPublishedIntegrationEvent() : base() { }

    public AnnouncementPublishedIntegrationEvent(Guid announcementId, string title, int type) : base()
    {
        AnnouncementId = announcementId;
        Title = title ?? string.Empty;
        Type = type;
    }
}
