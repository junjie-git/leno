using Leno.ReviewAfterSales.Application.DTOs;
using Leno.ReviewAfterSales.Application.Services;
using Leno.ReviewAfterSales.Domain.Aggregates;
using Leno.ReviewAfterSales.Domain.Repositories;
using Leno.ReviewAfterSales.Domain.Services;
using Leno.ReviewAfterSales.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReviewAggregate = Leno.ReviewAfterSales.Domain.Aggregates.Review;

namespace Leno.ReviewAfterSales.Application.Tests;

/// <summary>
/// 卖家查询本店铺商品评价列表（GetBySellerAsync）应用服务单元测试。
/// 验证：
/// - 成功场景：归属卖家分页查询返回 Approved 评价列表
/// - 卖家隔离场景：仓储层强制按 sellerId 过滤，卖家 A 查询不会返回卖家 B 的评价
/// - 商品名称过滤场景：经商品域 ACL 过滤 SpuId 列表后传入仓储
/// - 商品名称无匹配：返回空列表不查询数据库
/// - 评价域无 SPU：直接返回空列表不调用商品域 ACL
/// - 评分/回复状态/时间范围过滤透传仓储
/// </summary>
public sealed class ReviewGetBySellerTests
{
    private readonly Mock<IReviewRepository> _reviewRepoMock = new();
    private readonly Mock<IReviewEligibilityChecker> _eligibilityMock = new();
    private readonly Mock<IOrderStatusProvider> _orderStatusProviderMock = new();
    private readonly Mock<IProductInfoQueryService> _productInfoQueryServiceMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly ReviewAppService _sut;

    private static readonly Guid SellerId = Guid.NewGuid();
    private static readonly Guid OtherSellerId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid OrderLineId = Guid.NewGuid();
    private static readonly Guid SpuId1 = Guid.NewGuid();
    private static readonly Guid SpuId2 = Guid.NewGuid();
    private static readonly Guid SkuId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid AuditorId = Guid.NewGuid();

    public ReviewGetBySellerTests()
    {
        _sut = new ReviewAppService(
            _reviewRepoMock.Object,
            _eligibilityMock.Object,
            _orderStatusProviderMock.Object,
            _productInfoQueryServiceMock.Object,
            _uowMock.Object,
            NullLogger<ReviewAppService>.Instance);
    }

    #region Happy Path

    [Fact]
    public async Task GetBySellerAsync_NoProductName_ShouldQueryBySellerDirectly()
    {
        // Arrange: 无 productName 过滤，直接调用仓储按 sellerId 查询
        var reviews = new List<ReviewAggregate>
        {
            CreateApprovedReview(SpuId1, rating: 5, sellerId: SellerId),
            CreateApprovedReview(SpuId2, rating: 4, sellerId: SellerId)
        };
        _reviewRepoMock
            .Setup(r => r.QueryBySellerAsync(
                SellerId, null, null, null, null, null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reviews);
        _reviewRepoMock
            .Setup(r => r.CountBySellerAsync(
                SellerId, null, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        // Act
        var result = await _sut.GetBySellerAsync(
            SellerId, null, null, null, null, null, 1, 20, CancellationToken.None);

        // Assert
        result.Items.Should().HaveCount(2);
        result.Total.Should().Be(2);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
        // 无 productName 时不应查询 SPU 列表与商品域 ACL
        _reviewRepoMock.Verify(r => r.GetDistinctSpuIdsBySellerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _productInfoQueryServiceMock.Verify(p => p.GetProductNamesBySpuIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetBySellerAsync_WithRatingFilter_ShouldPassRatingToRepository()
    {
        // Arrange: 按评分=5 过滤
        var reviews = new List<ReviewAggregate> { CreateApprovedReview(SpuId1, rating: 5, sellerId: SellerId) };
        _reviewRepoMock
            .Setup(r => r.QueryBySellerAsync(
                SellerId, 5, null, null, null, null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reviews);
        _reviewRepoMock
            .Setup(r => r.CountBySellerAsync(
                SellerId, 5, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _sut.GetBySellerAsync(
            SellerId, 5, null, null, null, null, 1, 20, CancellationToken.None);

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items[0].Rating.Should().Be(5);
        _reviewRepoMock.Verify(r => r.QueryBySellerAsync(
            SellerId, 5, null, null, null, null, 1, 20, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetBySellerAsync_WithRepliedFilter_ShouldPassRepliedToRepository()
    {
        // Arrange: 按已回复过滤
        var review = CreateApprovedReview(SpuId1, rating: 5, sellerId: SellerId);
        review.SellerReply(SellerId, "感谢评价");
        var reviews = new List<ReviewAggregate> { review };
        _reviewRepoMock
            .Setup(r => r.QueryBySellerAsync(
                SellerId, null, true, null, null, null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reviews);
        _reviewRepoMock
            .Setup(r => r.CountBySellerAsync(
                SellerId, null, true, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _sut.GetBySellerAsync(
            SellerId, null, true, null, null, null, 1, 20, CancellationToken.None);

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items[0].SellerReplyContent.Should().Be("感谢评价");
        _reviewRepoMock.Verify(r => r.QueryBySellerAsync(
            SellerId, null, true, null, null, null, 1, 20, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetBySellerAsync_WithDateRange_ShouldPassDatesToRepository()
    {
        // Arrange: 按时间范围过滤
        var startDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2026, 7, 26, 0, 0, 0, DateTimeKind.Utc);
        var reviews = new List<ReviewAggregate> { CreateApprovedReview(SpuId1, rating: 5, sellerId: SellerId) };
        _reviewRepoMock
            .Setup(r => r.QueryBySellerAsync(
                SellerId, null, null, null, startDate, endDate, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reviews);
        _reviewRepoMock
            .Setup(r => r.CountBySellerAsync(
                SellerId, null, null, null, startDate, endDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _sut.GetBySellerAsync(
            SellerId, null, null, null, startDate, endDate, 1, 20, CancellationToken.None);

        // Assert
        result.Items.Should().HaveCount(1);
        _reviewRepoMock.Verify(r => r.QueryBySellerAsync(
            SellerId, null, null, null, startDate, endDate, 1, 20, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Product Name Filter via ACL

    [Fact]
    public async Task GetBySellerAsync_WithProductName_ShouldFilterSpuIdsViaAcl()
    {
        // Arrange: 卖家有 2 个 SPU，商品域 ACL 返回名称，按关键词过滤后匹配 1 个 SPU
        var sellerSpuIds = new List<Guid> { SpuId1, SpuId2 };
        _reviewRepoMock
            .Setup(r => r.GetDistinctSpuIdsBySellerAsync(SellerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sellerSpuIds);

        var productNameMap = new Dictionary<Guid, string>
        {
            { SpuId1, "Apple iPhone 15 Pro" },
            { SpuId2, "Samsung Galaxy S24" }
        };
        _productInfoQueryServiceMock
            .Setup(p => p.GetProductNamesBySpuIdsAsync(sellerSpuIds, It.IsAny<CancellationToken>()))
            .ReturnsAsync(productNameMap);

        var expectedFilteredSpuIds = new List<Guid> { SpuId1 };
        var reviews = new List<ReviewAggregate> { CreateApprovedReview(SpuId1, rating: 5, sellerId: SellerId) };
        _reviewRepoMock
            .Setup(r => r.QueryBySellerAsync(
                SellerId, null, null, expectedFilteredSpuIds, null, null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reviews);
        _reviewRepoMock
            .Setup(r => r.CountBySellerAsync(
                SellerId, null, null, expectedFilteredSpuIds, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _sut.GetBySellerAsync(
            SellerId, null, null, "iPhone", null, null, 1, 20, CancellationToken.None);

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items[0].SpuId.Should().Be(SpuId1);
        // 验证 SPU 列表前置查询与商品域 ACL 调用均触发
        _reviewRepoMock.Verify(r => r.GetDistinctSpuIdsBySellerAsync(SellerId, It.IsAny<CancellationToken>()), Times.Once);
        _productInfoQueryServiceMock.Verify(p => p.GetProductNamesBySpuIdsAsync(sellerSpuIds, It.IsAny<CancellationToken>()), Times.Once);
        // 验证传入仓储的 SpuId 列表为过滤后的列表
        _reviewRepoMock.Verify(r => r.QueryBySellerAsync(
            SellerId, null, null, It.Is<IReadOnlyList<Guid>?>(list => list != null && list.Count == 1 && list.Contains(SpuId1)),
            null, null, 1, 20, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetBySellerAsync_ProductNameNoMatch_ShouldReturnEmptyWithoutDbQuery()
    {
        // Arrange: 商品域 ACL 返回的名称均不匹配关键词，应直接返回空列表不查询数据库
        var sellerSpuIds = new List<Guid> { SpuId1, SpuId2 };
        _reviewRepoMock
            .Setup(r => r.GetDistinctSpuIdsBySellerAsync(SellerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sellerSpuIds);

        var productNameMap = new Dictionary<Guid, string>
        {
            { SpuId1, "Apple iPhone 15 Pro" },
            { SpuId2, "Samsung Galaxy S24" }
        };
        _productInfoQueryServiceMock
            .Setup(p => p.GetProductNamesBySpuIdsAsync(sellerSpuIds, It.IsAny<CancellationToken>()))
            .ReturnsAsync(productNameMap);

        // Act
        var result = await _sut.GetBySellerAsync(
            SellerId, null, null, "NonExistentProduct", null, null, 1, 20, CancellationToken.None);

        // Assert
        result.Items.Should().BeEmpty();
        result.Total.Should().Be(0);
        // 无匹配 SpuId 时不应调用仓储查询
        _reviewRepoMock.Verify(r => r.QueryBySellerAsync(
            It.IsAny<Guid>(), It.IsAny<int?>(), It.IsAny<bool?>(), It.IsAny<IReadOnlyList<Guid>?>(),
            It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _reviewRepoMock.Verify(r => r.CountBySellerAsync(
            It.IsAny<Guid>(), It.IsAny<int?>(), It.IsAny<bool?>(), It.IsAny<IReadOnlyList<Guid>?>(),
            It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetBySellerAsync_NoSellerSpu_ShouldReturnEmptyWithoutAclCall()
    {
        // Arrange: 卖家已通过评价关联的 SPU 列表为空，应直接返回空列表不调用商品域 ACL
        _reviewRepoMock
            .Setup(r => r.GetDistinctSpuIdsBySellerAsync(SellerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid>());

        // Act
        var result = await _sut.GetBySellerAsync(
            SellerId, null, null, "AnyProduct", null, null, 1, 20, CancellationToken.None);

        // Assert
        result.Items.Should().BeEmpty();
        result.Total.Should().Be(0);
        // 无 SPU 时不应调用商品域 ACL
        _productInfoQueryServiceMock.Verify(p => p.GetProductNamesBySpuIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
        // 也不应调用仓储查询
        _reviewRepoMock.Verify(r => r.QueryBySellerAsync(
            It.IsAny<Guid>(), It.IsAny<int?>(), It.IsAny<bool?>(), It.IsAny<IReadOnlyList<Guid>?>(),
            It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetBySellerAsync_WhitespaceProductName_ShouldBeTreatedAsNoFilter()
    {
        // Arrange: productName 为空白字符串应视为不过滤，直接调用仓储按 sellerId 查询
        var reviews = new List<ReviewAggregate> { CreateApprovedReview(SpuId1, rating: 5, sellerId: SellerId) };
        _reviewRepoMock
            .Setup(r => r.QueryBySellerAsync(
                SellerId, null, null, null, null, null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reviews);
        _reviewRepoMock
            .Setup(r => r.CountBySellerAsync(
                SellerId, null, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _sut.GetBySellerAsync(
            SellerId, null, null, "   ", null, null, 1, 20, CancellationToken.None);

        // Assert: 空白 productName 不触发 SPU 列表查询与 ACL
        result.Items.Should().HaveCount(1);
        _reviewRepoMock.Verify(r => r.GetDistinctSpuIdsBySellerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _productInfoQueryServiceMock.Verify(p => p.GetProductNamesBySpuIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Seller Isolation Scenarios

    [Fact]
    public async Task GetBySellerAsync_ShouldAlwaysFilterBySellerId_ForcingIsolation()
    {
        // Arrange: 验证仓储层始终按 sellerId 过滤，卖家 A 查询不会返回卖家 B 的评价
        // 即使攻击者尝试传入 OtherSellerId，仓储也只返回 OtherSellerId 的评价，不会泄露 SellerId 的数据
        var otherSellerReviews = new List<ReviewAggregate>
        {
            CreateApprovedReview(SpuId1, rating: 5, sellerId: OtherSellerId)
        };
        _reviewRepoMock
            .Setup(r => r.QueryBySellerAsync(
                OtherSellerId, null, null, null, null, null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(otherSellerReviews);
        _reviewRepoMock
            .Setup(r => r.CountBySellerAsync(
                OtherSellerId, null, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act: 攻击者以 OtherSellerId 查询
        var result = await _sut.GetBySellerAsync(
            OtherSellerId, null, null, null, null, null, 1, 20, CancellationToken.None);

        // Assert: 仅返回 OtherSellerId 的评价，强制隔离
        result.Items.Should().HaveCount(1);
        result.Items[0].SellerId.Should().Be(OtherSellerId);
        // 验证仓储被调用时传入的是 OtherSellerId（即 JWT 注入的 sellerId），不接受客户端伪造
        _reviewRepoMock.Verify(r => r.QueryBySellerAsync(
            OtherSellerId, null, null, null, null, null, 1, 20, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetBySellerAsync_SellerA_ShouldNotSeeSellerBReviews()
    {
        // Arrange: 卖家 A 查询，仓储返回卖家 A 的评价（不含卖家 B 的评价）
        var sellerAReviews = new List<ReviewAggregate>
        {
            CreateApprovedReview(SpuId1, rating: 5, sellerId: SellerId)
        };
        _reviewRepoMock
            .Setup(r => r.QueryBySellerAsync(
                SellerId, null, null, null, null, null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sellerAReviews);
        _reviewRepoMock
            .Setup(r => r.CountBySellerAsync(
                SellerId, null, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _sut.GetBySellerAsync(
            SellerId, null, null, null, null, null, 1, 20, CancellationToken.None);

        // Assert: 所有返回的评价均归属 SellerId（卖家 A），不混入卖家 B 的评价
        result.Items.Should().HaveCount(1);
        result.Items.Should().OnlyContain(r => r.SellerId == SellerId);
    }

    #endregion

    #region Helpers

    private static ReviewAggregate CreateApprovedReview(Guid spuId, int rating, Guid sellerId)
    {
        var review = ReviewAggregate.Create(
            Guid.NewGuid(), OrderId, OrderLineId, spuId, SkuId, UserId, rating,
            "评价内容", new List<string>(), sellerId);
        review.Approve(AuditorId);
        return review;
    }

    #endregion
}
