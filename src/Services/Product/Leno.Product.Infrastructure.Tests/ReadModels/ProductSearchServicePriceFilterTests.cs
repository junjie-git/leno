using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Leno.Infrastructure.ReadModel;
using Leno.Product.Application;
using Leno.Product.Infrastructure.ReadModels;
using Moq;

namespace Leno.Product.Infrastructure.Tests.ReadModels;

/// <summary>
/// P1-T6 单元测试：验证 <see cref="ProductSearchService"/> 价格区间过滤改用区间相交逻辑
/// （MinPrice ≤ maxPrice AND MaxPrice ≥ minPrice），而非仅校验 MinPrice 单一区间。
/// 通过 Mock &lt;IEsReadModelRepository&lt;ProductReadModel&gt;&gt; 捕获传入 SearchAsync 的查询回调，
/// 反射安全地遍历 Query 树断言嵌套 BoolQuery.Must 包含两条 NumberRangeQuery 且边界正确。
/// </summary>
public class ProductSearchServicePriceFilterTests
{
    /// <summary>
    /// 给定 minPrice=100、maxPrice=150，构建的查询应在 Filter 中包含一个嵌套 BoolQuery，
    /// 其 Must 含两条 NumberRangeQuery：MinPrice.Lte=150、MaxPrice.Gte=100（区间相交）。
    /// </summary>
    [Fact]
    public async Task SearchAsync_WithPriceRange_Should_BuildIntervalIntersectionQuery()
    {
        // Arrange
        var mockRepo = new Mock<IEsReadModelRepository<ProductReadModel>>();
        Func<QueryDescriptor<ProductReadModel>, Query>? capturedQuery = null;
        mockRepo.Setup(r => r.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<Func<QueryDescriptor<ProductReadModel>, Query>>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, Func<QueryDescriptor<ProductReadModel>, Query>, int, int, CancellationToken>(
                (_, q, _, _, _) => capturedQuery = q)
            .ReturnsAsync((Array.Empty<ProductReadModel>(), 0L));

        var service = new ProductSearchService(mockRepo.Object);

        // Act — 仅传价格区间，无关键词（触发纯 Filter 查询路径）
        await service.SearchAsync(
            keyword: null,
            categoryId: null,
            brandId: null,
            minPrice: 100m,
            maxPrice: 150m,
            sort: null,
            page: 1,
            pageSize: 20,
            ct: CancellationToken.None);

        // Assert
        capturedQuery.Should().NotBeNull();
        var query = capturedQuery!(new QueryDescriptor<ProductReadModel>());

        // 顶层为 BoolQuery（无关键词 → 纯 Filter 查询）
        var topBool = GetVariant<BoolQuery>(query, "Bool");
        topBool.Should().NotBeNull();
        var filters = topBool!.Filter;
        filters.Should().NotBeNull();

        // 价格区间应构建为嵌套 BoolQuery 的 Must（区间相交），而非单一 NumberRangeQuery 直接放入 Filter
        var nestedBools = filters!
            .Select(f => GetVariant<BoolQuery>(f, "Bool"))
            .Where(b => b is not null && b.Must is not null)
            .ToList();
        nestedBools.Should().HaveCount(1, "价格区间过滤应仅产生一个嵌套 BoolQuery");

        var priceBool = nestedBools[0]!;
        priceBool.Must.Should().HaveCount(2, "区间相交需两条 range query：MinPrice ≤ maxPrice AND MaxPrice ≥ minPrice");

        // 收集两条 NumberRangeQuery
        var ranges = priceBool.Must!
            .Select(q => GetVariant<NumberRangeQuery>(q, "NumberRange"))
            .Where(r => r is not null)
            .ToList();
        ranges.Should().HaveCount(2);

        // MinPrice range：Lte = maxPrice(150)，无 Gte
        var minPriceRange = ranges.FirstOrDefault(r => r!.Lte is not null && r!.Gte is null);
        minPriceRange.Should().NotBeNull("应存在 MinPrice ≤ maxPrice 的 range query");
        minPriceRange!.Lte.Should().Be(150.0);

        // MaxPrice range：Gte = minPrice(100)，无 Lte
        var maxPriceRange = ranges.FirstOrDefault(r => r!.Gte is not null && r!.Lte is null);
        maxPriceRange.Should().NotBeNull("应存在 MaxPrice ≥ minPrice 的 range query");
        maxPriceRange!.Gte.Should().Be(100.0);
    }

    /// <summary>
    /// 给定仅 minPrice=100（无 maxPrice），应构建 MaxPrice.Gte=100 的单边区间相交查询。
    /// </summary>
    [Fact]
    public async Task SearchAsync_WithOnlyMinPrice_Should_BuildMaxPriceGteRange()
    {
        // Arrange
        var mockRepo = new Mock<IEsReadModelRepository<ProductReadModel>>();
        Func<QueryDescriptor<ProductReadModel>, Query>? capturedQuery = null;
        mockRepo.Setup(r => r.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<Func<QueryDescriptor<ProductReadModel>, Query>>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, Func<QueryDescriptor<ProductReadModel>, Query>, int, int, CancellationToken>(
                (_, q, _, _, _) => capturedQuery = q)
            .ReturnsAsync((Array.Empty<ProductReadModel>(), 0L));

        var service = new ProductSearchService(mockRepo.Object);

        // Act
        await service.SearchAsync(
            keyword: null,
            categoryId: null,
            brandId: null,
            minPrice: 100m,
            maxPrice: null,
            sort: null,
            page: 1,
            pageSize: 20,
            ct: CancellationToken.None);

        // Assert
        var query = capturedQuery!(new QueryDescriptor<ProductReadModel>());
        var topBool = GetVariant<BoolQuery>(query, "Bool");
        var nestedBools = topBool!.Filter!
            .Select(f => GetVariant<BoolQuery>(f, "Bool"))
            .Where(b => b is not null && b.Must is not null)
            .ToList();

        nestedBools.Should().HaveCount(1);
        var ranges = nestedBools[0]!.Must!
            .Select(q => GetVariant<NumberRangeQuery>(q, "NumberRange"))
            .Where(r => r is not null)
            .ToList();
        ranges.Should().HaveCount(2);

        // 仅 minPrice：MaxPrice range Gte=100，MinPrice range 无 Lte（仅作为占位参与 Must 相交）
        var maxPriceRange = ranges.FirstOrDefault(r => r!.Gte is not null);
        maxPriceRange.Should().NotBeNull();
        maxPriceRange!.Gte.Should().Be(100.0);
    }

    /// <summary>
    /// 不传任何价格区间时，Filter 中不应出现价格相关的嵌套 BoolQuery。
    /// </summary>
    [Fact]
    public async Task SearchAsync_WithoutPriceRange_Should_NotBuildPriceBoolQuery()
    {
        // Arrange
        var mockRepo = new Mock<IEsReadModelRepository<ProductReadModel>>();
        Func<QueryDescriptor<ProductReadModel>, Query>? capturedQuery = null;
        mockRepo.Setup(r => r.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<Func<QueryDescriptor<ProductReadModel>, Query>>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, Func<QueryDescriptor<ProductReadModel>, Query>, int, int, CancellationToken>(
                (_, q, _, _, _) => capturedQuery = q)
            .ReturnsAsync((Array.Empty<ProductReadModel>(), 0L));

        var service = new ProductSearchService(mockRepo.Object);

        // Act
        await service.SearchAsync(
            keyword: null,
            categoryId: null,
            brandId: null,
            minPrice: null,
            maxPrice: null,
            sort: null,
            page: 1,
            pageSize: 20,
            ct: CancellationToken.None);

        // Assert
        var query = capturedQuery!(new QueryDescriptor<ProductReadModel>());
        var topBool = GetVariant<BoolQuery>(query, "Bool");
        var nestedBools = topBool!.Filter!
            .Select(f => GetVariant<BoolQuery>(f, "Bool"))
            .Where(b => b is not null && b.Must is not null)
            .ToList();
        nestedBools.Should().BeEmpty("无价格区间时不应构建价格嵌套 BoolQuery");
    }

    /// <summary>
    /// 反射安全地从 <see cref="Query"/> 中提取指定变体属性值，
    /// 兼容 Elastic .NET 客户端不同版本的 null/throw 取值语义。
    /// </summary>
    private static T? GetVariant<T>(object? source, string propertyName) where T : class
    {
        if (source is null)
        {
            return null;
        }

        var prop = source.GetType().GetProperty(propertyName);
        if (prop is null)
        {
            return null;
        }

        try
        {
            return prop.GetValue(source) as T;
        }
        catch
        {
            return null;
        }
    }
}
