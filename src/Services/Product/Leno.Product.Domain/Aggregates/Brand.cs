using Leno.Product.Domain.Exceptions;
using Leno.Product.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.Product.Domain.Aggregates;

/// <summary>
/// 商品品牌聚合根，由运营维护，卖家发布商品时选用。
/// 停用品牌不在卖家发布选项中出现，已挂载商品保留显示。
/// </summary>
public sealed class Brand : AggregateRoot
{
    private const int MaxNameLength = 50;
    private const int MaxLogoLength = 512;

    /// <summary>品牌名称。</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>品牌 Logo URL，可空。</summary>
    public string? Logo { get; private set; }

    /// <summary>品牌状态。</summary>
    public BrandStatus Status { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private Brand() { }

    private Brand(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，创建品牌，初始状态为启用。
    /// </summary>
    /// <param name="brandId">品牌标识，由应用层生成。</param>
    /// <param name="name">品牌名称。</param>
    /// <param name="logo">品牌 Logo URL，可空。</param>
    public static Brand Create(Guid brandId, string name, string? logo = null)
    {
        if (brandId == Guid.Empty)
        {
            throw new ProductDomainException("品牌标识不可为空", "BRAND_ID_EMPTY");
        }

        ValidateName(name);
        ValidateLogo(logo);

        return new Brand(brandId)
        {
            Name = name.Trim(),
            Logo = string.IsNullOrWhiteSpace(logo) ? null : logo.Trim(),
            Status = BrandStatus.Enabled
        };
    }

    /// <summary>
    /// 更新品牌名称与 Logo。
    /// </summary>
    public void Update(string name, string? logo = null)
    {
        ValidateName(name);
        ValidateLogo(logo);

        Name = name.Trim();
        Logo = string.IsNullOrWhiteSpace(logo) ? null : logo.Trim();
    }

    /// <summary>启用品牌。</summary>
    public void Enable()
    {
        Status = BrandStatus.Enabled;
    }

    /// <summary>停用品牌。</summary>
    public void Disable()
    {
        Status = BrandStatus.Disabled;
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ProductDomainException("品牌名称不可为空", "BRAND_NAME_EMPTY");
        }

        if (name.Trim().Length > MaxNameLength)
        {
            throw new ProductDomainException($"品牌名称长度不可超过 {MaxNameLength} 字符", "BRAND_NAME_LENGTH");
        }
    }

    private static void ValidateLogo(string? logo)
    {
        if (!string.IsNullOrWhiteSpace(logo) && logo.Trim().Length > MaxLogoLength)
        {
            throw new ProductDomainException($"Logo URL 长度不可超过 {MaxLogoLength} 字符", "BRAND_LOGO_LENGTH");
        }
    }
}
