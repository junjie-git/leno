using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Leno.Infrastructure.ReadModel;
using Leno.Product.Application;
using Leno.Product.Application.DTOs;
using Leno.SharedContracts.Responses;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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
    private readonly ILogger<ProductSearchService> _logger;

    public ProductSearchService(
        IEsReadModelRepository<ProductReadModel> repository,
        ILogger<ProductSearchService>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
        _logger = logger ?? NullLogger<ProductSearchService>.Instance;
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
        var safePage = page < 1 ? 1 : page;
        var safePageSize = pageSize is <= 0 or > 100 ? 20 : pageSize;
        var from = (safePage - 1) * safePageSize;

        // 修复审计 #7：sort 参数原被 _ = sort; 静默丢弃。
        // 现根据 sort 值构建 ES 排序配置回调：price_asc/price_desc 按 MinPrice 升/降序；
        // relevance/default/null/空 保持默认相关性得分；无效值记录警告并回退。
        var configure = BuildSortConfigure(sort);

        IReadOnlyList<ProductReadModel> items;
        long total;
        if (configure is null)
        {
            (items, total) = await _repository.SearchAsync(
                ProductIndexName,
                _ => BuildQuery(keyword, categoryId, brandId, minPrice, maxPrice),
                from,
                safePageSize,
                ct);
        }
        else
        {
            (items, total) = await _repository.SearchAsync(
                ProductIndexName,
                _ => BuildQuery(keyword, categoryId, brandId, minPrice, maxPrice),
                configure,
                from,
                safePageSize,
                ct);
        }

        var dtos = items.Select(ToDto).ToList();
        return new PageResult<ProductSearchResultDto>(dtos, (int)total, safePage, safePageSize);
    }

    /// <summary>
    /// 根据 sort 参数构建 ES 搜索请求配置回调。
    /// 返回 null 表示无需额外配置（走默认相关性得分排序）；非 null 表示需在搜索描述符上追加排序。
    /// 无效排序值或读模型不支持的排序字段（如 sales_desc）记录警告并回退到相关性排序。
    /// </summary>
    private Action<SearchRequestDescriptor<ProductReadModel>>? BuildSortConfigure(string? sort)
    {
        var normalized = sort?.Trim().ToLowerInvariant();

        switch (normalized)
        {
            case null:
            case "":
            case "relevance":
            case "default":
                return null;

            case "price_asc":
                return descriptor => descriptor.Sort(s => s.Field(
                    Infer.Field<ProductReadModel>(p => p.MinPrice),
                    o => o.Order(SortOrder.Asc)));

            case "price_desc":
                return descriptor => descriptor.Sort(s => s.Field(
                    Infer.Field<ProductReadModel>(p => p.MinPrice),
                    o => o.Order(SortOrder.Desc)));

            case "sales_desc":
                // 读模型当前无 SalesCount 字段，暂回退到相关性排序
                _logger.LogWarning("排序值 Sort={Sort} 对应的销量字段在读模型中不存在，回退到相关性排序", sort);
                return null;

            default:
                _logger.LogWarning("无效的排序值 Sort={Sort}，回退到相关性排序", sort);
                return null;
        }
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
