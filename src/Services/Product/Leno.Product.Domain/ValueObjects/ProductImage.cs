namespace Leno.Product.Domain.ValueObjects;

/// <summary>
/// 商品图片值对象，承载图片 URL、排序序号与是否主图标记。
/// 不可变，通过工厂方法创建。
/// </summary>
public sealed record ProductImage
{
    /// <summary>图片 URL。</summary>
    public string Url { get; private set; } = string.Empty;

    /// <summary>排序序号，越小越靠前。</summary>
    public int SortOrder { get; private set; }

    /// <summary>是否主图。</summary>
    public bool IsMain { get; private set; }

    private ProductImage() { }

    private ProductImage(string url, int sortOrder, bool isMain)
    {
        Url = url;
        SortOrder = sortOrder;
        IsMain = isMain;
    }

    /// <summary>
    /// 创建商品图片值对象。
    /// </summary>
    /// <param name="url">图片 URL，不可为空。</param>
    /// <param name="sortOrder">排序序号，须 ≥ 0。</param>
    /// <param name="isMain">是否主图。</param>
    public static ProductImage Create(string url, int sortOrder, bool isMain)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("图片 URL 不可为空", nameof(url));
        }

        if (sortOrder < 0)
        {
            throw new ArgumentException("排序序号不可为负", nameof(sortOrder));
        }

        return new ProductImage(url.Trim(), sortOrder, isMain);
    }
}
