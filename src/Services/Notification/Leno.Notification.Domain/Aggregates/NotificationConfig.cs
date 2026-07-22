using Leno.Notification.Domain.Exceptions;
using Leno.Notification.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.Notification.Domain.Aggregates;

/// <summary>
/// 通知渠道配置聚合根，按渠道 + 配置键维度持久化单项配置值。
/// <para>
/// 替代 IOptionsMonitor 仅只读绑定无法运行时更新的局限：
/// 运营端通过 <c>NotificationConfigAppService.UpdateConfigAsync</c> 写入本聚合后，
/// 基础设施层同步刷新 <c>ConsulReloadableConfigurationProvider</c> 触发 IOptionsMonitor 重载。
/// </para>
/// 聚合标识 <see cref="Entity.Id"/> 仅作主键，业务唯一键为 (Channel, ConfigKey)。
/// </summary>
public sealed class NotificationConfig : AggregateRoot
{
    /// <summary>通知渠道（业务唯一键的一部分）。</summary>
    public NotificationChannel Channel { get; private set; }

    /// <summary>配置键（如 Host、Port、Username、AccessKeyId）。</summary>
    public string ConfigKey { get; private set; } = string.Empty;

    /// <summary>配置值（敏感字段以明文存储，显示时由应用层脱敏）。</summary>
    public string ConfigValue { get; private set; } = string.Empty;

    /// <summary>配置描述（可选，用于运营端展示）。</summary>
    public string? Description { get; private set; }

    /// <summary>是否为敏感字段（密码、密钥等），用于审计日志脱敏判断。</summary>
    public bool IsSensitive { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private NotificationConfig() { }

    private NotificationConfig(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，创建渠道配置项。
    /// </summary>
    /// <param name="id">聚合标识。</param>
    /// <param name="channel">通知渠道。</param>
    /// <param name="configKey">配置键。</param>
    /// <param name="configValue">配置值。</param>
    /// <param name="description">配置描述（可选）。</param>
    /// <param name="isSensitive">是否为敏感字段。</param>
    public static NotificationConfig Create(
        Guid id,
        NotificationChannel channel,
        string configKey,
        string configValue,
        string? description = null,
        bool isSensitive = false)
    {
        if (id == Guid.Empty)
        {
            throw new NotificationDomainException("Id 不可为空", "NOTIFICATION_CONFIG_ID_EMPTY");
        }

        if (!Enum.IsDefined(channel))
        {
            throw new NotificationDomainException($"通知渠道非法：{channel}", "NOTIFICATION_CONFIG_CHANNEL_INVALID");
        }

        if (string.IsNullOrWhiteSpace(configKey))
        {
            throw new NotificationDomainException("配置键不可为空", "NOTIFICATION_CONFIG_KEY_EMPTY");
        }

        if (configValue is null)
        {
            throw new NotificationDomainException("配置值不可为 null", "NOTIFICATION_CONFIG_VALUE_NULL");
        }

        return new NotificationConfig(id)
        {
            Channel = channel,
            ConfigKey = configKey,
            ConfigValue = configValue,
            Description = description,
            IsSensitive = isSensitive
        };
    }

    /// <summary>
    /// 更新配置值。空字符串等价于清空配置。
    /// </summary>
    /// <param name="configValue">新配置值。</param>
    /// <param name="description">新描述（可选，null 表示不修改描述）。</param>
    public void UpdateValue(string configValue, string? description = null)
    {
        if (configValue is null)
        {
            throw new NotificationDomainException("配置值不可为 null", "NOTIFICATION_CONFIG_VALUE_NULL");
        }

        ConfigValue = configValue;

        if (description is not null)
        {
            Description = description;
        }
    }
}
