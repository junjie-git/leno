namespace Leno.Review.Application.Services;

/// <summary>
/// 商品信息查询防腐层接口，供评价域在卖家侧按商品名称过滤评价时反查 SPU 与商品名称映射。
/// 实际实现位于基础设施层，通过 gRPC 调用商品域 ProductInternalService.GetProductDetail。
/// 仅暴露卖家评价列表场景所需的方法子集，避免评价域直接依赖商品域实现细节。
/// </summary>
public interface IProductInfoQueryService
{
    /// <summary>
    /// 按 SPU 标识批量查询商品名称映射。
    /// 任一 SPU 查询失败返回 null（不抛异常），调用方按 null 跳过该 SPU 的名称过滤。
    /// </summary>
    /// <param name="spuIds">SPU 标识集合。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>SpuId → 商品名称 字典；查询失败的 SPU 不出现在字典中。</returns>
    Task<IReadOnlyDictionary<Guid, string>> GetProductNamesBySpuIdsAsync(
        IReadOnlyCollection<Guid> spuIds,
        CancellationToken ct = default);
}
