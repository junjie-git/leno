using Leno.SystemAdmin.Domain.Events;
using Leno.SystemAdmin.Domain.Exceptions;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.SystemAdmin.Domain.Aggregates;

/// <summary>
/// 特性开关聚合根，封装开关键、策略与规则的不变量，支持启停与评估上下文刷新。
/// 聚合标识 <see cref="Entity.Id"/> 即对外 <c>FlagId</c>。
/// 变更（启停/更新）附加 <see cref="FeatureFlagChangedEvent"/>，驱动各业务域刷新本地开关缓存。
/// </summary>
public sealed class FeatureFlag : AggregateRoot
{
    private const int MaxKeyLength = 128;
    private const int MaxNameLength = 128;
    private const int MaxDescriptionLength = 500;

    /// <summary>聚合标识，等同 <see cref="Entity.Id"/>。</summary>
    public Guid FlagId => Id;

    /// <summary>开关键，全局唯一，≤128 字。</summary>
    public string Key { get; private set; } = string.Empty;

    /// <summary>开关名称，≤128 字。</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>开关描述，≤500 字，可空。</summary>
    public string? Description { get; private set; }

    /// <summary>是否启用。</summary>
    public bool IsEnabled { get; private set; }

    /// <summary>评估策略。</summary>
    public FeatureFlagStrategy Strategy { get; private set; }

    /// <summary>规则 JSON 字符串（白名单/角色/比例等），领域层保持透明不解析。</summary>
    public string? Rules { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private FeatureFlag() { }

    private FeatureFlag(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，校验键/名称/策略，初始 IsEnabled=true。
    /// </summary>
    /// <param name="flagId">开关标识，由应用层生成。</param>
    /// <param name="key">开关键。</param>
    /// <param name="name">开关名称。</param>
    /// <param name="description">开关描述，可空。</param>
    /// <param name="strategy">评估策略。</param>
    /// <param name="rules">规则 JSON，可空。</param>
    public static FeatureFlag Create(Guid flagId, string key, string name, string? description, FeatureFlagStrategy strategy, string? rules)
    {
        if (flagId == Guid.Empty)
        {
            throw new SystemAdminDomainException("开关标识不可为空", "FLAG_ID_EMPTY");
        }

        ValidateKey(key);
        ValidateName(name);
        ValidateDescription(description);
        ValidateStrategy(strategy);

        return new FeatureFlag(flagId)
        {
            Key = key.Trim(),
            Name = name.Trim(),
            Description = NormalizeNullable(description),
            Strategy = strategy,
            Rules = NormalizeNullable(rules),
            IsEnabled = true
        };
    }

    /// <summary>
    /// 更新名称、描述、策略与规则，附加 <see cref="FeatureFlagChangedEvent"/>。
    /// </summary>
    /// <param name="name">开关名称。</param>
    /// <param name="description">开关描述，可空。</param>
    /// <param name="strategy">评估策略。</param>
    /// <param name="rules">规则 JSON，可空。</param>
    public void Update(string name, string? description, FeatureFlagStrategy strategy, string? rules)
    {
        ValidateName(name);
        ValidateDescription(description);
        ValidateStrategy(strategy);

        Name = name.Trim();
        Description = NormalizeNullable(description);
        Strategy = strategy;
        Rules = NormalizeNullable(rules);

        AddDomainEvent(new FeatureFlagChangedEvent(Id, Key, IsEnabled, (int)Strategy));
    }

    /// <summary>启用开关，附加 <see cref="FeatureFlagChangedEvent"/>。</summary>
    public void Enable()
    {
        IsEnabled = true;
        AddDomainEvent(new FeatureFlagChangedEvent(Id, Key, IsEnabled, (int)Strategy));
    }

    /// <summary>停用开关，附加 <see cref="FeatureFlagChangedEvent"/>。</summary>
    public void Disable()
    {
        IsEnabled = false;
        AddDomainEvent(new FeatureFlagChangedEvent(Id, Key, IsEnabled, (int)Strategy));
    }

    private static string? NormalizeNullable(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void ValidateKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new SystemAdminDomainException("开关键不可为空", "FLAG_KEY_EMPTY");
        }

        if (key.Trim().Length > MaxKeyLength)
        {
            throw new SystemAdminDomainException($"开关键长度不可超过 {MaxKeyLength} 字符", "FLAG_KEY_LENGTH");
        }
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new SystemAdminDomainException("开关名称不可为空", "FLAG_NAME_EMPTY");
        }

        if (name.Trim().Length > MaxNameLength)
        {
            throw new SystemAdminDomainException($"开关名称长度不可超过 {MaxNameLength} 字符", "FLAG_NAME_LENGTH");
        }
    }

    private static void ValidateDescription(string? description)
    {
        if (!string.IsNullOrWhiteSpace(description) && description.Trim().Length > MaxDescriptionLength)
        {
            throw new SystemAdminDomainException($"开关描述长度不可超过 {MaxDescriptionLength} 字符", "FLAG_DESC_LENGTH");
        }
    }

    private static void ValidateStrategy(FeatureFlagStrategy strategy)
    {
        if (!Enum.IsDefined(strategy))
        {
            throw new SystemAdminDomainException("评估策略取值非法", "FLAG_STRATEGY_INVALID");
        }
    }
}
