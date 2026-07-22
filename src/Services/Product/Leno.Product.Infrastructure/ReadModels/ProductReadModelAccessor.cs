using Leno.Infrastructure.ReadModel;
using Leno.Product.Application.Queries;

namespace Leno.Product.Infrastructure.ReadModels;

/// <summary>
/// 商品读模型访问器实现，基于 <see cref="IEsReadModelRepository{T}"/> 查询 ES 读模型。
/// 实现 Application 层定义的 <see cref="IProductReadModelAccessor"/> 端口，保持分层洁癖。
/// </summary>
public sealed class ProductReadModelAccessor : IProductReadModelAccessor
{
    private readonly IEsReadModelRepository<ProductReadModel> _repository;

    public ProductReadModelAccessor(IEsReadModelRepository<ProductReadModel> repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
    }

    /// <inheritdoc />
    public async Task<ProductDetailResult?> GetByIdAsync(Guid productId, CancellationToken ct = default)
    {
        if (productId == Guid.Empty)
        {
            return null;
        }

        var model = await _repository.GetByIdAsync(productId.ToString(), ProductSearchService.ProductIndexName, ct);
        return model is null ? null : ToResult(model);
    }

    private static ProductDetailResult ToResult(ProductReadModel model)
        => new()
        {
            ProductId = model.Id,
            Title = model.Title,
            Subtitle = model.Subtitle,
            MainImageUrl = model.MainImageUrl,
            CategoryId = model.CategoryId,
            BrandId = model.BrandId,
            ShopId = model.ShopId,
            Status = model.Status,
            Specs = model.Specs,
            MinPrice = model.MinPrice,
            MaxPrice = model.MaxPrice,
            Currency = model.Currency,
            // 修复审计 #18：映射 SKU 嵌套文档到读侧结果，供买家端详情页渲染 SKU 选择器
            Skus = model.Skus.Select(s => new SkuDetailResult
            {
                SkuId = s.SkuId,
                SkuCode = s.SkuCode,
                Price = s.Price,
                Currency = s.Currency,
                StockQty = s.StockQty,
                Status = s.Status,
                ImageUrl = s.ImageUrl,
                SpecAttributes = s.SpecAttributes.Select(a => new SkuSpecAttributeResult
                {
                    Name = a.Name,
                    Value = a.Value
                }).ToList()
            }).ToList(),
            Score = model.Score,
            ReviewCount = model.ReviewCount,
            IndexedAt = model.IndexedAt
        };
}
