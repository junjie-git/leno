using Leno.Product.Domain.ValueObjects;

namespace Leno.Product.Application.DTOs;

/// <summary>
/// 商品详情 DTO，返回 SPU 与其 SKU 集合。
/// </summary>
public sealed class ProductDto
{
    public Guid Id { get; init; }

    public Guid ShopId { get; init; }

    public Guid SellerId { get; init; }

    public string Title { get; init; } = string.Empty;

    public string? Subtitle { get; init; }

    public string MainImageUrl { get; init; } = string.Empty;

    public Guid CategoryId { get; init; }

    public Guid? BrandId { get; init; }

    public ProductStatus Status { get; init; }

    public IReadOnlyList<string> Specs { get; init; } = Array.Empty<string>();

    public IReadOnlyList<ProductImageDto> Images { get; init; } = Array.Empty<ProductImageDto>();

    public IReadOnlyList<SkuDto> Skus { get; init; } = Array.Empty<SkuDto>();

    public bool SuspendedByShop { get; init; }

    public Guid? ReviewedBy { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime UpdatedAt { get; init; }
}

/// <summary>
/// SKU 详情 DTO。
/// </summary>
public sealed class SkuDto
{
    public Guid Id { get; init; }

    public string SkuCode { get; init; } = string.Empty;

    public decimal Price { get; init; }

    public string Currency { get; init; } = string.Empty;

    public int StockQty { get; init; }

    public IReadOnlyList<SpecAttributeDto> SpecAttributes { get; init; } = Array.Empty<SpecAttributeDto>();

    public SkuStatus Status { get; init; }

    public string? ImageUrl { get; init; }
}

/// <summary>商品图片 DTO。</summary>
public sealed class ProductImageDto
{
    public string Url { get; init; } = string.Empty;

    public int SortOrder { get; init; }

    public bool IsMain { get; init; }
}

/// <summary>规格属性 DTO。</summary>
public sealed class SpecAttributeDto
{
    public string Name { get; init; } = string.Empty;

    public string Value { get; init; } = string.Empty;
}
