using Leno.SystemAdmin.Domain.Exceptions;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.SystemAdmin.Domain.Aggregates;

/// <summary>
/// 字典项实体，作为 <see cref="DataDictionary"/> 聚合内的实体而非独立聚合根。
/// 编码在同字典内唯一、值与标签非空等不变量由 <see cref="DataDictionary"/> 聚合根统一保证。
/// 工厂与变更方法为 internal，仅聚合根可创建与变更。
/// </summary>
public sealed class DictionaryItem : Entity
{
    private const int MaxCodeLength = 64;
    private const int MaxLabelLength = 128;
    private const int MaxValueLength = 256;

    /// <summary>所属字典标识。</summary>
    public Guid DictionaryId { get; private set; }

    /// <summary>字典项编码，同字典下唯一，≤64 字。</summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>显示标签，≤128 字。</summary>
    public string Label { get; private set; } = string.Empty;

    /// <summary>字典项值，≤256 字。</summary>
    public string Value { get; private set; } = string.Empty;

    /// <summary>排序序号，升序展示。</summary>
    public int SortOrder { get; private set; }

    /// <summary>启停状态。</summary>
    public DictionaryStatus Status { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private DictionaryItem() { }

    private DictionaryItem(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，校验编码/标签/值，初始状态为 Enabled。仅供聚合根调用。
    /// </summary>
    /// <param name="itemId">字典项标识。</param>
    /// <param name="dictionaryId">所属字典标识。</param>
    /// <param name="code">字典项编码。</param>
    /// <param name="label">显示标签。</param>
    /// <param name="value">字典项值。</param>
    /// <param name="sortOrder">排序序号。</param>
    internal static DictionaryItem Create(Guid itemId, Guid dictionaryId, string code, string label, string value, int sortOrder)
    {
        if (itemId == Guid.Empty)
        {
            throw new SystemAdminDomainException("字典项标识不可为空", "DICT_ITEM_ID_EMPTY");
        }

        if (dictionaryId == Guid.Empty)
        {
            throw new SystemAdminDomainException("字典标识不可为空", "DICT_ITEM_DICT_EMPTY");
        }

        ValidateCode(code);
        ValidateLabel(label);
        ValidateValue(value);

        return new DictionaryItem(itemId)
        {
            DictionaryId = dictionaryId,
            Code = code.Trim(),
            Label = label.Trim(),
            Value = value,
            SortOrder = sortOrder,
            Status = DictionaryStatus.Enabled
        };
    }

    /// <summary>
    /// 更新标签、值与排序序号。仅供聚合根调用。
    /// </summary>
    /// <param name="label">显示标签。</param>
    /// <param name="value">字典项值。</param>
    /// <param name="sortOrder">排序序号。</param>
    internal void Update(string label, string value, int sortOrder)
    {
        ValidateLabel(label);
        ValidateValue(value);

        Label = label.Trim();
        Value = value;
        SortOrder = sortOrder;
    }

    /// <summary>启用字典项。仅供聚合根调用。</summary>
    internal void Enable()
    {
        Status = DictionaryStatus.Enabled;
    }

    /// <summary>停用字典项。仅供聚合根调用。</summary>
    internal void Disable()
    {
        Status = DictionaryStatus.Disabled;
    }

    private static void ValidateCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new SystemAdminDomainException("字典项编码不可为空", "DICT_ITEM_CODE_EMPTY");
        }

        if (code.Trim().Length > MaxCodeLength)
        {
            throw new SystemAdminDomainException($"字典项编码长度不可超过 {MaxCodeLength} 字符", "DICT_ITEM_CODE_LENGTH");
        }
    }

    private static void ValidateLabel(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            throw new SystemAdminDomainException("字典项标签不可为空", "DICT_ITEM_LABEL_EMPTY");
        }

        if (label.Trim().Length > MaxLabelLength)
        {
            throw new SystemAdminDomainException($"字典项标签长度不可超过 {MaxLabelLength} 字符", "DICT_ITEM_LABEL_LENGTH");
        }
    }

    private static void ValidateValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new SystemAdminDomainException("字典项值不可为空", "DICT_ITEM_VALUE_EMPTY");
        }

        if (value.Length > MaxValueLength)
        {
            throw new SystemAdminDomainException($"字典项值长度不可超过 {MaxValueLength} 字符", "DICT_ITEM_VALUE_LENGTH");
        }
    }
}
