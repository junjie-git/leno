using Leno.Product.Domain.Exceptions;
using Leno.Product.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.Product.Domain.Aggregates;

/// <summary>
/// 商品分类聚合根，支持多级分类树（自引用 ParentId，Level 限制最大 3 级）。
/// 启用态分类可挂载商品；停用态在买家侧不展示，已挂载商品保留显示。
/// </summary>
public sealed class Category : AggregateRoot
{
    private const int MaxLevel = 3;
    private const int MaxNameLength = 50;
    private const int MaxSortOrder = 9999;

    /// <summary>分类名称。</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>父分类标识，顶级分类为 null。</summary>
    public Guid? ParentId { get; private set; }

    /// <summary>分类层级，1-3。</summary>
    public int Level { get; private set; }

    /// <summary>排序序号，越小越靠前。</summary>
    public int SortOrder { get; private set; }

    /// <summary>分类状态。</summary>
    public CategoryStatus Status { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private Category() { }

    private Category(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，创建分类。父分类为 null 时创建顶级分类（Level=1），否则子分类 Level=父 Level+1。
    /// </summary>
    /// <param name="categoryId">分类标识，由应用层生成。</param>
    /// <param name="name">分类名称。</param>
    /// <param name="parentId">父分类标识，可空。</param>
    /// <param name="parentLevel">父分类层级，无父分类时传 null。</param>
    /// <param name="sortOrder">排序序号。</param>
    public static Category Create(
        Guid categoryId,
        string name,
        Guid? parentId = null,
        int? parentLevel = null,
        int sortOrder = 0)
    {
        if (categoryId == Guid.Empty)
        {
            throw new ProductDomainException("分类标识不可为空", "CATEGORY_ID_EMPTY");
        }

        ValidateName(name);
        ValidateSortOrder(sortOrder);

        var level = parentLevel is null ? 1 : parentLevel.Value + 1;
        if (level is < 1 or > MaxLevel)
        {
            throw new ProductDomainException($"分类层级须为 1-{MaxLevel}", "CATEGORY_LEVEL_INVALID");
        }

        if (parentId.HasValue && parentId.Value == Guid.Empty)
        {
            throw new ProductDomainException("父分类标识不可为空", "CATEGORY_PARENT_EMPTY");
        }

        return new Category(categoryId)
        {
            Name = name.Trim(),
            ParentId = parentId.HasValue && parentId.Value != Guid.Empty ? parentId : null,
            Level = level,
            SortOrder = sortOrder,
            Status = CategoryStatus.Enabled
        };
    }

    /// <summary>
    /// 更新分类名称与排序序号。
    /// </summary>
    public void Update(string name, int sortOrder)
    {
        ValidateName(name);
        ValidateSortOrder(sortOrder);

        Name = name.Trim();
        SortOrder = sortOrder;
    }

    /// <summary>启用分类。</summary>
    public void Enable()
    {
        Status = CategoryStatus.Enabled;
    }

    /// <summary>停用分类。</summary>
    public void Disable()
    {
        Status = CategoryStatus.Disabled;
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ProductDomainException("分类名称不可为空", "CATEGORY_NAME_EMPTY");
        }

        if (name.Trim().Length > MaxNameLength)
        {
            throw new ProductDomainException($"分类名称长度不可超过 {MaxNameLength} 字符", "CATEGORY_NAME_LENGTH");
        }
    }

    private static void ValidateSortOrder(int sortOrder)
    {
        if (sortOrder is < 0 or > MaxSortOrder)
        {
            throw new ProductDomainException($"排序序号须为 0-{MaxSortOrder}", "CATEGORY_SORT_ORDER_INVALID");
        }
    }
}
