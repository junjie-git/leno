using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Leno.Infrastructure.ReadModel;
using Leno.Product.Application;
using Leno.Product.Infrastructure.ReadModels;
using Moq;

namespace Leno.Product.Infrastructure.Tests.ReadModels;

/// <summary>
/// P1-T7 单元测试：验证 <see cref="ProductSearchService"/> 的 sort 参数不再被静默忽略。
/// price_asc/price_desc 通过带 configure 回调的新 <c>SearchAsync</c> 重载传递排序选项，
/// 并在 ES 搜索描述符上构建按 MinPrice 升/降序的 SortOptions；
/// null/relevance/default/无效值/不支持字段 走原 5 参重载（默认相关性得分排序）。
/// 通过 Mock&lt;IEsReadModelRepository&lt;ProductReadModel&gt;&gt; 同时设置两个重载，
/// 捕获 configure 回调后在新 <see cref="SearchRequestDescriptor{T}"/> 上调用，
/// 反射安全读取 SortOptions.Order 断言排序方向。
/// </summary>
public class ProductSearchServiceSortTests
{
    /// <summary>
    /// sort=price_asc 时应走带 configure 的新重载（非原 5 参重载），
    /// 且 configure 在搜索描述符上构建按 MinPrice 升序的单条 SortOptions。
    /// </summary>
    [Fact]
    public async Task SearchAsync_WithPriceAsc_Should_BuildAscendingSortViaConfigureOverload()
    {
        var mockRepo = new Mock<IEsReadModelRepository<ProductReadModel>>();
        var tracker = SetupMockWithTracking(mockRepo);

        var service = new ProductSearchService(mockRepo.Object);

        await service.SearchAsync(
            keyword: null,
            categoryId: null,
            brandId: null,
            minPrice: null,
            maxPrice: null,
            sort: "price_asc",
            page: 1,
            pageSize: 20,
            ct: CancellationToken.None);

        tracker.PlainOverloadCalled.Should().BeFalse("price_asc 不应走无排序的原 5 参重载");
        tracker.ConfigureOverloadCalled.Should().BeTrue("price_asc 应走带 configure 回调的新重载");
        tracker.CapturedConfigure.Should().NotBeNull("price_asc 应产生排序配置回调");

        var sortList = InvokeConfigureAndGetSortList(tracker.CapturedConfigure!);
        sortList.Should().NotBeNull("configure 回调应在搜索描述符上设置 Sort");
        sortList!.Count.Should().Be(1, "应仅构建一条排序选项");
        GetSortOrderValue(sortList[0]!).Should().Be(SortOrder.Asc, "price_asc 应按升序排序");
    }

    /// <summary>
    /// sort=price_desc 时应走带 configure 的新重载，
    /// 且 configure 在搜索描述符上构建按 MinPrice 降序的单条 SortOptions。
    /// </summary>
    [Fact]
    public async Task SearchAsync_WithPriceDesc_Should_BuildDescendingSortViaConfigureOverload()
    {
        var mockRepo = new Mock<IEsReadModelRepository<ProductReadModel>>();
        var tracker = SetupMockWithTracking(mockRepo);

        var service = new ProductSearchService(mockRepo.Object);

        await service.SearchAsync(
            keyword: null,
            categoryId: null,
            brandId: null,
            minPrice: null,
            maxPrice: null,
            sort: "price_desc",
            page: 1,
            pageSize: 20,
            ct: CancellationToken.None);

        tracker.PlainOverloadCalled.Should().BeFalse("price_desc 不应走无排序的原 5 参重载");
        tracker.ConfigureOverloadCalled.Should().BeTrue("price_desc 应走带 configure 回调的新重载");
        tracker.CapturedConfigure.Should().NotBeNull();

        var sortList = InvokeConfigureAndGetSortList(tracker.CapturedConfigure!);
        sortList.Should().NotBeNull();
        sortList!.Count.Should().Be(1);
        GetSortOrderValue(sortList[0]!).Should().Be(SortOrder.Desc, "price_desc 应按降序排序");
    }

    /// <summary>
    /// sort 大小写不敏感：PRICE_ASC 应等价于 price_asc，走 configure 重载并按升序排序。
    /// </summary>
    [Fact]
    public async Task SearchAsync_WithUpperCasePriceAsc_Should_BeCaseInsensitive()
    {
        var mockRepo = new Mock<IEsReadModelRepository<ProductReadModel>>();
        var tracker = SetupMockWithTracking(mockRepo);

        var service = new ProductSearchService(mockRepo.Object);

        await service.SearchAsync(
            keyword: null,
            categoryId: null,
            brandId: null,
            minPrice: null,
            maxPrice: null,
            sort: "PRICE_ASC",
            page: 1,
            pageSize: 20,
            ct: CancellationToken.None);

        tracker.ConfigureOverloadCalled.Should().BeTrue("PRICE_ASC 经 ToLowerInvariant 后应匹配 price_asc");
        tracker.PlainOverloadCalled.Should().BeFalse();

        var sortList = InvokeConfigureAndGetSortList(tracker.CapturedConfigure!);
        sortList.Should().NotBeNull();
        sortList!.Count.Should().Be(1);
        GetSortOrderValue(sortList[0]!).Should().Be(SortOrder.Asc);
    }

    /// <summary>
    /// sort 为 null/空/relevance/default 时应走原 5 参重载（默认相关性得分排序），不传 configure。
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("relevance")]
    [InlineData("default")]
    [InlineData("DEFAULT")]
    public async Task SearchAsync_WithDefaultSort_Should_UsePlainOverload(string? sort)
    {
        var mockRepo = new Mock<IEsReadModelRepository<ProductReadModel>>();
        var tracker = SetupMockWithTracking(mockRepo);

        var service = new ProductSearchService(mockRepo.Object);

        await service.SearchAsync(
            keyword: null,
            categoryId: null,
            brandId: null,
            minPrice: null,
            maxPrice: null,
            sort: sort,
            page: 1,
            pageSize: 20,
            ct: CancellationToken.None);

        tracker.PlainOverloadCalled.Should().BeTrue("默认相关性排序应走原 5 参重载");
        tracker.ConfigureOverloadCalled.Should().BeFalse("默认排序不应触发 configure 重载");
    }

    /// <summary>
    /// sort 为无效值或读模型不支持的排序字段（sales_desc）时，
    /// 应记录警告并回退到原 5 参重载（默认相关性得分排序）。
    /// </summary>
    [Theory]
    [InlineData("invalid_value")]
    [InlineData("sales_desc")]
    [InlineData("random")]
    public async Task SearchAsync_WithUnsupportedSort_Should_FallbackToPlainOverload(string? sort)
    {
        var mockRepo = new Mock<IEsReadModelRepository<ProductReadModel>>();
        var tracker = SetupMockWithTracking(mockRepo);

        var service = new ProductSearchService(mockRepo.Object);

        await service.SearchAsync(
            keyword: null,
            categoryId: null,
            brandId: null,
            minPrice: null,
            maxPrice: null,
            sort: sort,
            page: 1,
            pageSize: 20,
            ct: CancellationToken.None);

        tracker.PlainOverloadCalled.Should().BeTrue("无效/不支持的排序值应回退到默认相关性排序");
        tracker.ConfigureOverloadCalled.Should().BeFalse("回退时不应触发 configure 重载");
    }

    /// <summary>
    /// 为 Mock 仓储同时设置原 5 参重载和带 configure 的新重载，
    /// 返回 <see cref="OverloadTracker"/> 记录哪个重载被调用及捕获的 configure 回调。
    /// </summary>
    private static OverloadTracker SetupMockWithTracking(Mock<IEsReadModelRepository<ProductReadModel>> mockRepo)
    {
        var tracker = new OverloadTracker();

        // 原 5 参重载（无 configure）
        mockRepo.Setup(r => r.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<Func<QueryDescriptor<ProductReadModel>, Query>>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => tracker.PlainOverloadCalled = true)
            .ReturnsAsync((Array.Empty<ProductReadModel>(), 0L));

        // 带 configure 的新重载
        mockRepo.Setup(r => r.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<Func<QueryDescriptor<ProductReadModel>, Query>>(),
                It.IsAny<Action<SearchRequestDescriptor<ProductReadModel>>?>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, Func<QueryDescriptor<ProductReadModel>, Query>, Action<SearchRequestDescriptor<ProductReadModel>>?, int, int, CancellationToken>(
                (_, _, cfg, _, _, _) =>
                {
                    tracker.CapturedConfigure = cfg;
                    tracker.ConfigureOverloadCalled = true;
                })
            .ReturnsAsync((Array.Empty<ProductReadModel>(), 0L));

        return tracker;
    }

    /// <summary>
    /// 在新的 <see cref="SearchRequestDescriptor{T}"/> 上调用 configure 回调，
    /// 返回描述符上设置的 Sort 列表（非泛型 IList 以兼容反射读取）。
    /// </summary>
    private static System.Collections.IList? InvokeConfigureAndGetSortList(
        Action<SearchRequestDescriptor<ProductReadModel>> configure)
    {
        var descriptor = new SearchRequestDescriptor<ProductReadModel>();
        configure(descriptor);
        // SearchRequestDescriptor<T> 继承自 SearchRequest，Sort 属性类型为 IList<SortOptions>?
        var sortProperty = typeof(SearchRequest).GetProperty("Sort");
        return sortProperty?.GetValue(descriptor) as System.Collections.IList;
    }

    /// <summary>
    /// 反射安全地从 SortOptions 实例读取 Order 属性值，
    /// 兼容 Elastic .NET 客户端不同版本的属性命名。
    /// </summary>
    private static object? GetSortOrderValue(object sortOption)
    {
        var prop = sortOption.GetType().GetProperty("Order");
        return prop?.GetValue(sortOption);
    }

    /// <summary>
    /// 跟踪哪个 SearchAsync 重载被调用及捕获的 configure 回调。
    /// </summary>
    private sealed class OverloadTracker
    {
        public bool PlainOverloadCalled;
        public bool ConfigureOverloadCalled;
        public Action<SearchRequestDescriptor<ProductReadModel>>? CapturedConfigure;
    }
}
