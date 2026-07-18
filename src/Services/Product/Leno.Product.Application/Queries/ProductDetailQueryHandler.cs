using Leno.Infrastructure.Abstractions.Cqrs;

namespace Leno.Product.Application.Queries;

/// <summary>
/// 买家端商品详情查询处理器。
/// 经 <see cref="IProductReadModelAccessor"/>（端口由 Infrastructure 层 <c>ProductReadModelAccessor</c> 实现）
/// 查询 ES 读模型并返回 <see cref="ProductDetailResult"/>。
/// 双发期 2 周内与 <c>SPUAppService.GetByIdAsync</c> 并存，2 周后 Controller 切换到本 QueryHandler。
/// </summary>
public sealed class ProductDetailQueryHandler : IQueryHandler<ProductDetailQuery, ProductDetailResult?>
{
    private readonly IProductReadModelAccessor _readModelAccessor;

    public ProductDetailQueryHandler(IProductReadModelAccessor readModelAccessor)
    {
        ArgumentNullException.ThrowIfNull(readModelAccessor);
        _readModelAccessor = readModelAccessor;
    }

    /// <inheritdoc />
    public Task<ProductDetailResult?> HandleAsync(ProductDetailQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        // CurrentUserId 当前用于个性化字段的预留扩展点（如用户专属价）；本实现暂不消费。
        _ = query.CurrentUserId;

        return _readModelAccessor.GetByIdAsync(query.ProductId, ct);
    }
}
