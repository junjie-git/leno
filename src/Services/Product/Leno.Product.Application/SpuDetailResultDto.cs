namespace Leno.Product.Application;

/// <summary>
/// SPU 详情查询结果 DTO（跨 BC 内部查询，含 SKU 集合）。
/// 由 <see cref="IProductInternalQueryService.GetSpuDetailAsync"/> 返回。
/// </summary>
public sealed record SpuDetailResultDto(
    Guid SpuId,
    Guid SellerId,
    Guid? ShopId,
    string Title,
    string Subtitle,
    string MainImageUrl,
    string Description,
    IReadOnlyList<SpuSkuDto> Skus);

/// <summary>
/// SPU 详情内嵌的 SKU 概要信息。
/// </summary>
public sealed record SpuSkuDto(
    Guid SkuId,
    string SkuCode,
    string Title,
    string MainImageUrl,
    decimal Price,
    string Currency,
    int Stock,
    string Status);
