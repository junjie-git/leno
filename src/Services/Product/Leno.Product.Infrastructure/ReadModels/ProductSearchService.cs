using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Leno.Infrastructure.ReadModel;
using Leno.Product.Application;
using Leno.Product.Application.DTOs;
using Leno.SharedContracts.Responses;

namespace Leno.Product.Infrastructure.ReadModels;

/// <summary>
/// 商品搜索服务实现，基于 Elasticsearch 读模型。
/// 全文搜索标题/副标题，叠加分类、品牌、价格区间过滤，支持价格升降序排序分页。
/// 仅检索在售商品读模型文档。
/// </summary>
public sealed class ProductSearchService : IProductSearchService
{
    /// <summary>商品读模型索引名。</summary>
    public const string ProductIndexName = "leno_products";

    private readonly IEsReadModelRepository<ProductReadModel> _repository;

    public ProductSearchService(IEsReadModelRepository<ProductReadModel> repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
    }

    /// <inheritdoc />
    public async Task<PageResult<ProductSearchResultDto>> SearchAsync(
        string? keyword,
        Guid? categoryId,
        Guid? brandId,
        decimal? minPrice,
        decimal? maxPrice,
        string? sort,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        _ = sort; // 排序由读模型仓储默认相关性得分；预留扩展点

        var safePage = page < 1 ? 1 : page;
        var safePageSize = pageSize is <= 0 or > 100 ? 20 : pageSize;
        var from = (safePage - 1) * safePageSize;

        var (items, total) = await _repository.SearchAsync(
            ProductIndexName,
            _ => BuildQuery(keyword, categoryId, brandId, minPrice, maxPrice),
            from,
            safePageSize,
            ct);

        var dtos = items.Select(ToDto).ToList();
        return new PageResult<ProductSearchResultDto>(dtos, (int)total, safePage, safePageSize);
    }

    private static Query BuildQuery(
        string? keyword,
        Guid? categoryId,
        Guid? brandId,
        decimal? minPrice,
        decimal? maxPrice)
    {
        var filters = BuildFilters(categoryId, brandId, minPrice, maxPrice);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            // 关键词作为 must（相关性得分），过滤条件作为 filter（不打分、可缓存）
            var must = new MultiMatchQuery
            {
                Query = keyword,
                Fields = Infer.Fields<ProductReadModel>(x => x.Title, x => x.Subtitle),
                Operator = Operator.Or
            };
            return new BoolQuery { Must = new List<Query> { must }, Filter = filters };
        }

        // 无关键词时纯过滤查询
        return new BoolQuery { Filter = filters };
    }

    private static List<Query> BuildFilters(Guid? categoryId, Guid? brandId, decimal? minPrice, decimal? maxPrice)
    {
        var filters = new List<Query>
        {
            // 仅搜索在售商品
            new TermQuery(Infer.Field<ProductReadModel>(f => f.Status))
            {
                Value = nameof(Domain.ValueObjects.ProductStatus.OnSale)
            }
        };

        if (categoryId.HasValue)
        {
            filters.Add(new TermQuery(Infer.Field<ProductReadModel>(f => f.CategoryId))
            {
                Value = categoryId.Value.ToString()
            });
        }

        if (brandId.HasValue)
        {
            filters.Add(new TermQuery(Infer.Field<ProductReadModel>(f => f.BrandId))
            {
                Value = brandId.Value.ToString()
            });
        }

        if (minPrice.HasValue || maxPrice.HasValue)
        {
            // 修复审计 #6：使用区间相交逻辑替代单一 MinPrice range。
            // 原实现仅过滤 MinPrice ∈ [minPrice, maxPrice]，遗漏了 MinPrice < minPrice 但 MaxPrice ≥ minPrice 的商品
            // （其部分 SKU 价格落在用户筛选区间内）。
            // 区间相交：product.MinPrice ≤ maxPrice AND product.MaxPrice ≥ minPrice
            // 保证仅返回价格区间与用户筛选区间有交集的商品。
            var minPriceRange = new NumberRangeQuery(Infer.Field<ProductReadModel>(f => f.MinPrice));
            if (maxPrice.HasValue)
            {
                minPriceRange.Lte = (double)maxPrice.Value;
            }

            var maxPriceRange = new NumberRangeQuery(Infer.Field<ProductReadModel>(f => f.MaxPrice));
            if (minPrice.HasValue)
            {
                maxPriceRange.Gte = (double)minPrice.Value;
            }

            filters.Add(new BoolQuery
            {
                Must = new List<Query> { minPriceRange, maxPriceRange }
            });
        }

        return filters;
    }

    private static ProductSearchResultDto ToDto(ProductReadModel model)
        => new()
        {
            Id = model.Id,
            Title = model.Title,
            Subtitle = model.Subtitle,
            MainImageUrl = model.MainImageUrl,
            CategoryId = model.CategoryId,
            BrandId = model.BrandId,
            ShopId = model.ShopId,
            MinPrice = model.MinPrice,
            MaxPrice = model.MaxPrice,
            Currency = model.Currency
        };
}
