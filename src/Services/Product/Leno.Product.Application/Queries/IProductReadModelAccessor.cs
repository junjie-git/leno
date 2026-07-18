namespace Leno.Product.Application.Queries;

/// <summary>
/// 商品读模型访问器抽象（CQRS 读侧端口）。
/// 定义在 Application 层以保持分层洁癖：Application 不直接引用 Infrastructure 层的
/// <c>IEsReadModelRepository&lt;ProductReadModel&gt;</c>，由 Infrastructure 层实现。
/// </summary>
public interface IProductReadModelAccessor
{
    /// <summary>
    /// 按商品（SPU）标识查询 ES 读模型并映射为 <see cref="ProductDetailResult"/>。
    /// </summary>
    /// <param name="productId">商品（SPU）标识。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>读模型存在则返回 <see cref="ProductDetailResult"/>，否则返回 null。</returns>
    Task<ProductDetailResult?> GetByIdAsync(Guid productId, CancellationToken ct = default);
}
