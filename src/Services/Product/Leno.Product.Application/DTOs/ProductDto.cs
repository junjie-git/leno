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

    public IReadOnlyList<AuditInfoDto> AuditHistory { get; init; } = Array.Empty<AuditInfoDto>();

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

/// <summary>审核历史 DTO。</summary>
public sealed class AuditInfoDto
{
    public string OperatorId { get; init; } = string.Empty;

    public string OperatorName { get; init; } = string.Empty;

    public string Result { get; init; } = string.Empty;

    public string? Reason { get; init; }

    public DateTime AuditedAt { get; init; }
}

/// <summary>价格变更记录 DTO。</summary>
public sealed class PriceChangeRecordDto
{
    public string SkuId { get; init; } = string.Empty;

    public decimal OldPrice { get; init; }

    public decimal NewPrice { get; init; }

    public DateTime ChangedAt { get; init; }

    public string ChangedBy { get; init; } = string.Empty;

    /// <summary>变更原因，可空。修复审计 #13/#19：原 DTO 缺少此字段。</summary>
    public string? Reason { get; init; }
}

/// <summary>库存操作记录 DTO。</summary>
public sealed class StockOperationRecordDto
{
    public string SkuId { get; init; } = string.Empty;

    public string Operator { get; init; } = string.Empty;

    public int Delta { get; init; }

    public int NewStock { get; init; }

    public DateTime OperatedAt { get; init; }
}

/// <summary>调整价格 DTO。</summary>
public sealed class AdjustPriceDto
{
    /// <summary>新价格，须 > 0。</summary>
    public decimal Price { get; init; }

    /// <summary>币种，默认 CNY。</summary>
    public string Currency { get; init; } = "CNY";

    /// <summary>变更原因，可空但鼓励填写，用于价格审计。修复审计 #13。</summary>
    public string? Reason { get; init; }
}

/// <summary>库存调整 DTO。</summary>
public sealed class UpdateStockDto
{
    /// <summary>库存变动量（正数补货，负数扣减）。</summary>
    public int Delta { get; init; }
}
