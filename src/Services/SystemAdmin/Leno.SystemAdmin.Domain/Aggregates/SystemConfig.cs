using Leno.SystemAdmin.Domain.Events;
using Leno.SystemAdmin.Domain.Exceptions;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.SystemAdmin.Domain.Aggregates;

/// <summary>
/// 系统配置聚合根，键值对形式存储全局配置，支持分组、加密标记与启停。
/// 聚合标识 <see cref="Entity.Id"/> 即对外 <c>ConfigId</c>。
/// 变更（更新值/启停）附加 <see cref="ConfigChangedEvent"/>，驱动各业务域刷新本地配置缓存。
/// </summary>
public sealed class SystemConfig : AggregateRoot
{
    private const int MaxKeyLength = 128;
    private const int MaxGroupLength = 64;
    private const int MaxDescriptionLength = 500;

    /// <summary>聚合标识，等同 <see cref="Entity.Id"/>。</summary>
    public Guid ConfigId => Id;

    /// <summary>配置键，全局唯一，≤128 字。</summary>
    public string Key { get; private set; } = string.Empty;

    /// <summary>配置值。</summary>
    public string Value { get; private set; } = string.Empty;

    /// <summary>配置分组，≤64 字。</summary>
    public string Group { get; private set; } = string.Empty;

    /// <summary>配置描述，≤500 字，可空。</summary>
    public string? Description { get; private set; }

    /// <summary>是否加密存储。</summary>
    public bool IsEncrypted { get; private set; }

    /// <summary>启停状态。</summary>
    public ConfigStatus Status { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private SystemConfig() { }

    private SystemConfig(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，校验键/值/分组/描述，初始状态为 Enabled。
    /// </summary>
    /// <param name="configId">配置标识，由应用层生成。</param>
    /// <param name="key">配置键。</param>
    /// <param name="value">配置值。</param>
    /// <param name="group">配置分组。</param>
    /// <param name="description">配置描述，可空。</param>
    /// <param name="isEncrypted">是否加密存储。</param>
    public static SystemConfig Create(Guid configId, string key, string value, string group, string? description, bool isEncrypted)
    {
        if (configId == Guid.Empty)
        {
            throw new SystemAdminDomainException("配置标识不可为空", "CONFIG_ID_EMPTY");
        }

        ValidateKey(key);
        ValidateValue(value);
        ValidateGroup(group);
        ValidateDescription(description);

        return new SystemConfig(configId)
        {
            Key = key.Trim(),
            Value = value,
            Group = group.Trim(),
            Description = NormalizeNullable(description),
            IsEncrypted = isEncrypted,
            Status = ConfigStatus.Enabled
        };
    }

    /// <summary>
    /// 更新值、描述与加密标记（键不可变），附加 <see cref="ConfigChangedEvent"/>。
    /// </summary>
    /// <param name="value">配置值。</param>
    /// <param name="description">配置描述，可空。</param>
    /// <param name="isEncrypted">是否加密存储。</param>
    public void Update(string value, string? description, bool isEncrypted)
    {
        ValidateValue(value);
        ValidateDescription(description);

        Value = value;
        Description = NormalizeNullable(description);
        IsEncrypted = isEncrypted;

        AddDomainEvent(new ConfigChangedEvent(Id, Key, Value));
    }

    /// <summary>启用配置，附加 <see cref="ConfigChangedEvent"/>。</summary>
    public void Enable()
    {
        Status = ConfigStatus.Enabled;
        AddDomainEvent(new ConfigChangedEvent(Id, Key, Value));
    }

    /// <summary>停用配置，附加 <see cref="ConfigChangedEvent"/>。</summary>
    public void Disable()
    {
        Status = ConfigStatus.Disabled;
        AddDomainEvent(new ConfigChangedEvent(Id, Key, Value));
    }

    private static string? NormalizeNullable(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void ValidateKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new SystemAdminDomainException("配置键不可为空", "CONFIG_KEY_EMPTY");
        }

        if (key.Trim().Length > MaxKeyLength)
        {
            throw new SystemAdminDomainException($"配置键长度不可超过 {MaxKeyLength} 字符", "CONFIG_KEY_LENGTH");
        }
    }

    private static void ValidateValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new SystemAdminDomainException("配置值不可为空", "CONFIG_VALUE_EMPTY");
        }
    }

    private static void ValidateGroup(string group)
    {
        if (string.IsNullOrWhiteSpace(group))
        {
            throw new SystemAdminDomainException("配置分组不可为空", "CONFIG_GROUP_EMPTY");
        }

        if (group.Trim().Length > MaxGroupLength)
        {
            throw new SystemAdminDomainException($"配置分组长度不可超过 {MaxGroupLength} 字符", "CONFIG_GROUP_LENGTH");
        }
    }

    private static void ValidateDescription(string? description)
    {
        if (!string.IsNullOrWhiteSpace(description) && description.Trim().Length > MaxDescriptionLength)
        {
            throw new SystemAdminDomainException($"配置描述长度不可超过 {MaxDescriptionLength} 字符", "CONFIG_DESC_LENGTH");
        }
    }
}
