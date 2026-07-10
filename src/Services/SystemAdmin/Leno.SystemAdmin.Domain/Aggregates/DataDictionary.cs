using System.Diagnostics.CodeAnalysis;
using Leno.SystemAdmin.Domain.Exceptions;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.SystemAdmin.Domain.Aggregates;

/// <summary>
/// 数据字典聚合根，管理字典基础信息与字典项子集合的不变量。
/// 聚合标识 <see cref="Entity.Id"/> 即对外 <c>DictionaryId</c>。
/// 字典项 <see cref="DictionaryItem"/> 为聚合内实体，仅经聚合根 AddItem/RemoveItem/UpdateItem 维护。
/// </summary>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "'DataDictionary' 为系统管理域核心聚合名称，'Dictionary' 后缀表达领域语义（系统配置字典）而非 .NET 集合类型，被仓储、应用层与 API 契约广泛引用。")]
public sealed class DataDictionary : AggregateRoot
{
    private const int MaxCodeLength = 64;
    private const int MaxNameLength = 128;
    private const int MaxDescriptionLength = 500;

    private List<DictionaryItem> _items = [];

    /// <summary>聚合标识，等同 <see cref="Entity.Id"/>。</summary>
    public Guid DictionaryId => Id;

    /// <summary>字典编码，全局唯一，≤64 字。</summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>字典名称，≤128 字。</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>字典描述，≤500 字，可空。</summary>
    public string? Description { get; private set; }

    /// <summary>启停状态。</summary>
    public DictionaryStatus Status { get; private set; }

    /// <summary>
    /// 字典项集合，聚合内实体，仅经聚合根维护。
    /// 持久化为聚合子集合，故以可赋值 List 暴露给 EF Core，私有 setter 阻止外部整体替换。
    /// </summary>
    public List<DictionaryItem> Items { get => _items; private set => _items = value ?? []; }

    /// <summary>EF Core 无参构造。</summary>
    private DataDictionary() { }

    private DataDictionary(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，校验编码/名称/描述，初始状态为 Enabled，字典项为空集合。
    /// </summary>
    /// <param name="dictionaryId">字典标识，由应用层生成。</param>
    /// <param name="code">字典编码。</param>
    /// <param name="name">字典名称。</param>
    /// <param name="description">字典描述，可空。</param>
    public static DataDictionary Create(Guid dictionaryId, string code, string name, string? description)
    {
        if (dictionaryId == Guid.Empty)
        {
            throw new SystemAdminDomainException("字典标识不可为空", "DICT_ID_EMPTY");
        }

        ValidateCode(code);
        ValidateName(name);
        ValidateDescription(description);

        return new DataDictionary(dictionaryId)
        {
            Code = code.Trim(),
            Name = name.Trim(),
            Description = NormalizeNullable(description),
            Status = DictionaryStatus.Enabled
        };
    }

    /// <summary>
    /// 更新字典名称与描述（编码不可变）。
    /// </summary>
    /// <param name="name">字典名称。</param>
    /// <param name="description">字典描述，可空。</param>
    public void Update(string name, string? description)
    {
        ValidateName(name);
        ValidateDescription(description);

        Name = name.Trim();
        Description = NormalizeNullable(description);
    }

    /// <summary>启用字典。</summary>
    public void Enable()
    {
        Status = DictionaryStatus.Enabled;
    }

    /// <summary>停用字典。</summary>
    public void Disable()
    {
        Status = DictionaryStatus.Disabled;
    }

    /// <summary>
    /// 新增字典项，校验编码在本字典内唯一。
    /// </summary>
    /// <param name="itemId">字典项标识，由应用层生成。</param>
    /// <param name="code">字典项编码。</param>
    /// <param name="label">字典项显示标签。</param>
    /// <param name="value">字典项值。</param>
    /// <param name="sortOrder">排序序号。</param>
    public void AddItem(Guid itemId, string code, string label, string value, int sortOrder)
    {
        if (itemId == Guid.Empty)
        {
            throw new SystemAdminDomainException("字典项标识不可为空", "DICT_ITEM_ID_EMPTY");
        }

        var item = DictionaryItem.Create(itemId, Id, code, label, value, sortOrder);
        if (_items.Any(i => string.Equals(i.Code, item.Code, StringComparison.OrdinalIgnoreCase)))
        {
            throw new SystemAdminDomainException($"字典项编码已存在: {item.Code}", "DICT_ITEM_CODE_DUPLICATE", 409);
        }

        _items.Add(item);
    }

    /// <summary>
    /// 移除指定字典项，不存在则忽略（幂等）。
    /// </summary>
    /// <param name="itemId">字典项标识。</param>
    public void RemoveItem(Guid itemId)
    {
        var item = _items.FirstOrDefault(i => i.Id == itemId);
        if (item is not null)
        {
            _items.Remove(item);
        }
    }

    /// <summary>
    /// 更新指定字典项的标签、值与排序序号，不存在抛领域异常。
    /// </summary>
    /// <param name="itemId">字典项标识。</param>
    /// <param name="label">字典项显示标签。</param>
    /// <param name="value">字典项值。</param>
    /// <param name="sortOrder">排序序号。</param>
    public void UpdateItem(Guid itemId, string label, string value, int sortOrder)
    {
        var item = _items.FirstOrDefault(i => i.Id == itemId)
                   ?? throw new SystemAdminDomainException("字典项不存在", "DICT_ITEM_NOT_FOUND", 404);

        item.Update(label, value, sortOrder);
    }

    private static string? NormalizeNullable(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void ValidateCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new SystemAdminDomainException("字典编码不可为空", "DICT_CODE_EMPTY");
        }

        if (code.Trim().Length > MaxCodeLength)
        {
            throw new SystemAdminDomainException($"字典编码长度不可超过 {MaxCodeLength} 字符", "DICT_CODE_LENGTH");
        }
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new SystemAdminDomainException("字典名称不可为空", "DICT_NAME_EMPTY");
        }

        if (name.Trim().Length > MaxNameLength)
        {
            throw new SystemAdminDomainException($"字典名称长度不可超过 {MaxNameLength} 字符", "DICT_NAME_LENGTH");
        }
    }

    private static void ValidateDescription(string? description)
    {
        if (!string.IsNullOrWhiteSpace(description) && description.Trim().Length > MaxDescriptionLength)
        {
            throw new SystemAdminDomainException($"字典描述长度不可超过 {MaxDescriptionLength} 字符", "DICT_DESC_LENGTH");
        }
    }
}
