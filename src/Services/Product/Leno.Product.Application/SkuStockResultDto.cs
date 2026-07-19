namespace Leno.Product.Application;

/// <summary>
/// SKU 库存查询结果 DTO（跨 BC 内部查询）。
/// 由 <see cref="IProductInternalQueryService.GetSkuStockAsync"/> 返回，权威值取自 StockBaseline 聚合。
/// </summary>
public sealed record SkuStockResultDto(Guid SkuId, int Available, int Reserved);
