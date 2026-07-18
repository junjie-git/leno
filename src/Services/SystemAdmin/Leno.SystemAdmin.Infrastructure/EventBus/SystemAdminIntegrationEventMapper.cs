using Leno.Infrastructure.EventBus;
using Leno.SharedContracts.Events;
using Leno.SystemAdmin.Domain.Events;

namespace Leno.SystemAdmin.Infrastructure.EventBus;

/// <summary>
/// SystemAdmin BC 领域事件到集成事件的翻译器。
/// 将 FeatureFlag/SystemConfig/SystemAnnouncement 聚合根收集的领域事件翻译为 SharedContracts 中的集成事件。
/// </summary>
public class SystemAdminIntegrationEventMapper : IntegrationEventMapperBase
{
    public SystemAdminIntegrationEventMapper()
    {
        // FeatureFlagChangedEvent → FeatureFlagChangedIntegrationEvent（各业务域刷新本地开关缓存）
        RegisterHandler<FeatureFlagChangedEvent, FeatureFlagChangedIntegrationEvent>(e =>
            new FeatureFlagChangedIntegrationEvent(e.FlagId, e.FlagKey, e.IsEnabled, e.Strategy));

        // ConfigChangedEvent → ConfigChangedIntegrationEvent（各业务域刷新本地配置缓存）
        RegisterHandler<ConfigChangedEvent, ConfigChangedIntegrationEvent>(e =>
            new ConfigChangedIntegrationEvent(e.ConfigId, e.ConfigKey, e.ConfigValue));

        // AnnouncementPublishedEvent → AnnouncementPublishedIntegrationEvent（消息通知域推送公告）
        RegisterHandler<AnnouncementPublishedEvent, AnnouncementPublishedIntegrationEvent>(e =>
            new AnnouncementPublishedIntegrationEvent(e.AnnouncementId, e.Title, e.Type));
    }
}
