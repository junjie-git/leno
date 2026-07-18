namespace Leno.Product.Application.Queries;

/// <summary>
/// 买家端商品详情查询参数（CQRS 读侧 Query）。
/// 由 <see cref="ProductDetailQueryHandler"/> 处理，经 <c>IProductReadModelAccessor</c> 走 ES 读模型。
/// </summary>
public sealed class ProductDetailQuery
{
    /// <summary>商品（SPU）标识。</summary>
    public Guid ProductId { get; init; }

    /// <summary>当前用户标识，用于个性化字段（如用户专属价），可空表示匿名访问。</summary>
    public Guid? CurrentUserId { get; init; }
}
