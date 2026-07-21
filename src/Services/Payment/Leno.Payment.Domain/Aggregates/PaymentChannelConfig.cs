using Leno.Payment.Domain.Events;
using Leno.Payment.Domain.Exceptions;
using Leno.Payment.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.Payment.Domain.Aggregates;

/// <summary>
/// 支付渠道配置聚合根，管理支付渠道参数（如密钥、回调地址等）的增删改查。
/// 聚合标识 <see cref="Entity.Id"/> 即对外 <c>ConfigId</c>。
/// </summary>
public sealed class PaymentChannelConfig : AggregateRoot
{
    private const int MaxConfigNameLength = 128;
    private const int MaxConfigValueLength = 4096;
    private const int MaxDescriptionLength = 500;

    /// <summary>支付渠道。</summary>
    public PaymentChannel Channel { get; private set; }

    /// <summary>配置项名称。</summary>
    public string ConfigName { get; private set; } = string.Empty;

    /// <summary>配置项值（加密存储）。</summary>
    public string ConfigValue { get; private set; } = string.Empty;

    /// <summary>配置项描述。</summary>
    public string? Description { get; private set; }

    /// <summary>是否启用。</summary>
    public bool Enabled { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private PaymentChannelConfig() { }

    private PaymentChannelConfig(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，创建支付渠道配置项。
    /// </summary>
    /// <param name="id">配置标识，由应用层生成。</param>
    /// <param name="channel">支付渠道，不可为默认值。</param>
    /// <param name="configName">配置项名称，不可为空。</param>
    /// <param name="configValue">配置项值（加密后），不可为空。</param>
    /// <param name="description">配置项描述，可空。</param>
    public static PaymentChannelConfig Create(
        Guid id,
        PaymentChannel channel,
        string configName,
        string configValue,
        string? description)
    {
        if (id == Guid.Empty)
        {
            throw new PaymentDomainException("ConfigId 不可为空", "CHANNEL_CONFIG_ID_EMPTY");
        }

        if (!Enum.IsDefined<PaymentChannel>(channel))
        {
            throw new PaymentDomainException("支付渠道不可为默认值", "CHANNEL_CONFIG_CHANNEL_INVALID");
        }

        if (string.IsNullOrWhiteSpace(configName))
        {
            throw new PaymentDomainException("配置项名称不可为空", "CHANNEL_CONFIG_NAME_EMPTY");
        }

        if (configName.Length > MaxConfigNameLength)
        {
            throw new PaymentDomainException(
                $"配置项名称长度不可超过 {MaxConfigNameLength} 字符", "CHANNEL_CONFIG_NAME_LENGTH");
        }

        if (string.IsNullOrWhiteSpace(configValue))
        {
            throw new PaymentDomainException("配置项值不可为空", "CHANNEL_CONFIG_VALUE_EMPTY");
        }

        if (configValue.Length > MaxConfigValueLength)
        {
            throw new PaymentDomainException(
                $"配置项值长度不可超过 {MaxConfigValueLength} 字符", "CHANNEL_CONFIG_VALUE_LENGTH");
        }

        if (description is not null && description.Length > MaxDescriptionLength)
        {
            throw new PaymentDomainException(
                $"描述长度不可超过 {MaxDescriptionLength} 字符", "CHANNEL_CONFIG_DESC_LENGTH");
        }

        return new PaymentChannelConfig(id)
        {
            Channel = channel,
            ConfigName = configName,
            ConfigValue = configValue,
            Description = description,
            Enabled = true
        };
    }

    /// <summary>
    /// 启用当前配置项，并发布 <see cref="PaymentChannelConfigChangedDomainEvent"/>。
    /// </summary>
    public void Enable()
    {
        if (Enabled)
        {
            throw new PaymentDomainException("配置项已启用", "CHANNEL_CONFIG_ALREADY_ENABLED");
        }

        Enabled = true;
        AddDomainEvent(new PaymentChannelConfigChangedDomainEvent(Id, Channel.ToString(), ConfigName, "Enabled"));
    }

    /// <summary>
    /// 禁用当前配置项，并发布 <see cref="PaymentChannelConfigChangedDomainEvent"/>。
    /// </summary>
    public void Disable()
    {
        if (!Enabled)
        {
            throw new PaymentDomainException("配置项已禁用", "CHANNEL_CONFIG_ALREADY_DISABLED");
        }

        Enabled = false;
        AddDomainEvent(new PaymentChannelConfigChangedDomainEvent(Id, Channel.ToString(), ConfigName, "Disabled"));
    }

    /// <summary>
    /// 更新配置项值（加密后），并发布 <see cref="PaymentChannelConfigChangedDomainEvent"/>。
    /// </summary>
    /// <param name="newValue">新的配置项值，不可为空。</param>
    public void UpdateConfigValue(string newValue)
    {
        if (string.IsNullOrWhiteSpace(newValue))
        {
            throw new PaymentDomainException("配置项值不可为空", "CHANNEL_CONFIG_VALUE_EMPTY");
        }

        if (newValue.Length > MaxConfigValueLength)
        {
            throw new PaymentDomainException(
                $"配置项值长度不可超过 {MaxConfigValueLength} 字符", "CHANNEL_CONFIG_VALUE_LENGTH");
        }

        ConfigValue = newValue;
        AddDomainEvent(new PaymentChannelConfigChangedDomainEvent(Id, Channel.ToString(), ConfigName, "Updated"));
    }

    /// <summary>
    /// 更新配置项描述，并发布 <see cref="PaymentChannelConfigChangedDomainEvent"/>。
    /// 描述为空时清空，长度不可超过 <see cref="MaxDescriptionLength"/> 字符。
    /// </summary>
    /// <param name="description">新的配置项描述，可空。</param>
    public void UpdateDescription(string? description)
    {
        if (description is not null && description.Length > MaxDescriptionLength)
        {
            throw new PaymentDomainException(
                $"描述长度不可超过 {MaxDescriptionLength} 字符", "CHANNEL_CONFIG_DESC_LENGTH");
        }

        Description = description;
        AddDomainEvent(new PaymentChannelConfigChangedDomainEvent(Id, Channel.ToString(), ConfigName, "DescriptionUpdated"));
    }
}
