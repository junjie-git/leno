namespace Leno.Product.Application.DTOs;

/// <summary>
/// 创建商品（草稿）DTO。
/// </summary>
public sealed class CreateProductDto
{
    public string Title { get; init; } = string.Empty;

    public string? Subtitle { get; init; }

    public string MainImageUrl { get; init; } = string.Empty;

    public Guid CategoryId { get; init; }

    public Guid? BrandId { get; init; }

    public IReadOnlyList<string> Specs { get; init; } = Array.Empty<string>();

    public IReadOnlyList<ProductImageDto> Images { get; init; } = Array.Empty<ProductImageDto>();
}

/// <summary>
/// 更新商品基础信息 DTO。
/// </summary>
public sealed class UpdateProductDto
{
    public string Title { get; init; } = string.Empty;

    public string? Subtitle { get; init; }

    public string MainImageUrl { get; init; } = string.Empty;

    public Guid CategoryId { get; init; }

    public Guid? BrandId { get; init; }

    public IReadOnlyList<ProductImageDto> Images { get; init; } = Array.Empty<ProductImageDto>();
}

/// <summary>
/// 新增 SKU DTO。
/// </summary>
public sealed class AddSkuDto
{
    public string SkuCode { get; init; } = string.Empty;

    public decimal Price { get; init; }

    public string Currency { get; init; } = "CNY";

    public int StockQty { get; init; }

    public IReadOnlyList<SpecAttributeDto> SpecAttributes { get; init; } = Array.Empty<SpecAttributeDto>();

    public string? ImageUrl { get; init; }
}
