using Leno.Infrastructure.Abstractions.Cqrs;

namespace Leno.Product.Application.Queries;

/// <summary>
/// 买家端商品详情查询处理器。
/// 经 <see cref="IProductReadModelAccessor"/>（端口由 Infrastructure 层 <c>ProductReadModelAccessor</c> 实现）
/// 查询 ES 读模型并返回 <see cref="ProductDetailResult"/>。
/// <para>
/// 定位（双轨下线 DEC-9 改判，2026-09-21）：本查询是<b>买家端 ES 读模型路径</b>；
/// 管理端/通用详情走 <c>SPUAppService.GetByIdAsync</c>（SQL，含完整 SKU）。
/// 二者是用途不同的并行读路径，而非新旧实现 —— ES/SQL 终局归属由 E3 单独决策。
/// </para>
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
