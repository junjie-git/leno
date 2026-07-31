namespace Leno.SellerShop.Application.Services;

/// <summary>
/// 商品域防腐层服务接口（卖家店铺域视角）。
/// 仅暴露卖家归属校验所需的商品域查询能力，屏蔽商品域内部模型。
/// 接口定义在应用层，实现位于基础设施层（GrpcProductAntiCorruptionClient）。
/// </summary>
public interface IProductAntiCorruptionService
{
    /// <summary>
    /// 按 SPU 标识反查其归属的卖家标识。
    /// 用于卖家资源归属校验（resourceType=spu）：比对调用方声明的 sellerId 与 SPU 实际归属卖家。
    /// </summary>
    /// <param name="spuId">SPU 标识。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>SPU 归属卖家标识；SPU 不存在或防腐层调用失败时返回 null（fail-closed，由调用方判 false）。</returns>
    Task<Guid?> GetSpuSellerIdAsync(Guid spuId, CancellationToken ct = default);

    /// <summary>
    /// 查询指定店铺的低库存 SKU 列表（StockQty &lt; threshold）。
    /// 经 gRPC 调商品域 ProductInternalService.GetLowStockByShop。
    /// </summary>
    /// <param name="shopId">店铺标识。</param>
    /// <param name="threshold">低库存阈值。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>低库存 SKU 列表；ACL 调用失败时返回空列表（fail-soft，避免工作台白屏）。</returns>
    Task<List<LowStockItemDto>> GetLowStockSkusAsync(Guid shopId, int threshold, CancellationToken ct = default);

    /// <summary>
    /// 按店铺与日期范围查询商品销量数据（用于数据导出 ProductSales 报表）。
    /// 经 gRPC 调商品域聚合商品销量指标，返回表头与行数据以供文件生成器渲染。
    /// 实现位于 Task 13（GrpcProductAntiCorruptionClient）。
    /// </summary>
    /// <param name="shopId">店铺标识。</param>
    /// <param name="startDate">起始日期（UTC）。</param>
    /// <param name="endDate">结束日期（UTC）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>表头列表与行数据列表；ACL 调用失败时返回空列表（fail-soft）。</returns>
    Task<(List<string> Headers, List<IReadOnlyDictionary<string, object?>> Rows)> GetProductSalesAsync(
        Guid shopId, DateTime startDate, DateTime endDate, CancellationToken ct = default);
}
