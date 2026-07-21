using Leno.SellerShop.Application.Services;
using Leno.SellerShop.Domain.Aggregates;
using Leno.SellerShop.Domain.Repositories;
using Leno.SellerShop.Infrastructure.ReadModels;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Leno.SellerShop.Infrastructure.Tests.ReadModels;

/// <summary>
/// ShopDashboardReadModelBuilder 单元测试。
/// 验证 builder 从 ShopDashboardData 聚合读取 ConfirmedOrders/CancelledOrders，
/// 从 IReviewAntiCorruptionService 读取评论统计（TotalReviews/AverageRating/FiveStarReviews/OneStarReviews），
/// fail-closed 时按零值兜底；不再硬编码 6 个字段为 0。
/// </summary>
public sealed class ShopDashboardReadModelBuilderTests
{
    private readonly Mock<IShopRepository> _shopRepositoryMock = new();
    private readonly Mock<IShopDashboardRepository> _dashboardRepositoryMock = new();
    private readonly Mock<IReviewAntiCorruptionService> _reviewAclMock = new();
    private readonly ILogger<ShopDashboardReadModelBuilder> _logger =
        NullLogger<ShopDashboardReadModelBuilder>.Instance;

    [Fact]
    public async Task BuildAsync_Should_Populate_ConfirmedOrders_From_Dashboard_Aggregate()
    {
        // Arrange — 模拟已支付 3 笔订单，ConfirmedOrders 应为 3
        var shopId = Guid.NewGuid();
        var shop = Shop.Create(shopId, Guid.NewGuid(), "测试店铺", "13800138000");
        var dashboard = ShopDashboardData.Create(shopId);
        dashboard.OnOrderPaid(100m);
        dashboard.OnOrderPaid(200m);
        dashboard.OnOrderPaid(300m);

        _shopRepositoryMock.Setup(r => r.GetByIdAsync(shopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shop);
        _dashboardRepositoryMock.Setup(r => r.GetByShopIdAsync(shopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dashboard);
        _reviewAclMock.Setup(a => a.GetReviewStatisticsAsync(shopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReviewStatisticsDto { TotalReviews = 0, AverageRating = 0m, FiveStarReviews = 0, OneStarReviews = 0 });

        var builder = new ShopDashboardReadModelBuilder(
            _shopRepositoryMock.Object,
            _dashboardRepositoryMock.Object,
            _reviewAclMock.Object,
            _logger);

        // Act
        var result = await builder.BuildAsync(shopId, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.ConfirmedOrders);
        Assert.Equal(600m, result.TotalSales);
    }

    [Fact]
    public async Task BuildAsync_Should_Populate_CancelledOrders_From_Dashboard_Aggregate()
    {
        // Arrange — 模拟 2 笔订单创建后取消，CancelledOrders 应为 2
        var shopId = Guid.NewGuid();
        var shop = Shop.Create(shopId, Guid.NewGuid(), "测试店铺", "13800138000");
        var dashboard = ShopDashboardData.Create(shopId);
        dashboard.OnOrderCreated();
        dashboard.OnOrderCancelled();
        dashboard.OnOrderCreated();
        dashboard.OnOrderCancelled();

        _shopRepositoryMock.Setup(r => r.GetByIdAsync(shopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shop);
        _dashboardRepositoryMock.Setup(r => r.GetByShopIdAsync(shopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dashboard);
        _reviewAclMock.Setup(a => a.GetReviewStatisticsAsync(shopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReviewStatisticsDto { TotalReviews = 0, AverageRating = 0m, FiveStarReviews = 0, OneStarReviews = 0 });

        var builder = new ShopDashboardReadModelBuilder(
            _shopRepositoryMock.Object,
            _dashboardRepositoryMock.Object,
            _reviewAclMock.Object,
            _logger);

        // Act
        var result = await builder.BuildAsync(shopId, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.CancelledOrders);
        Assert.Equal(2, result.TotalOrders);
    }

    [Fact]
    public async Task BuildAsync_Should_Populate_Review_Statistics_From_Acl()
    {
        // Arrange — 防腐层返回真实评论统计
        var shopId = Guid.NewGuid();
        var shop = Shop.Create(shopId, Guid.NewGuid(), "测试店铺", "13800138000");
        var dashboard = ShopDashboardData.Create(shopId);

        _shopRepositoryMock.Setup(r => r.GetByIdAsync(shopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shop);
        _dashboardRepositoryMock.Setup(r => r.GetByShopIdAsync(shopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dashboard);
        _reviewAclMock.Setup(a => a.GetReviewStatisticsAsync(shopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReviewStatisticsDto
            {
                TotalReviews = 50,
                AverageRating = 4.2m,
                FiveStarReviews = 30,
                OneStarReviews = 5
            });

        var builder = new ShopDashboardReadModelBuilder(
            _shopRepositoryMock.Object,
            _dashboardRepositoryMock.Object,
            _reviewAclMock.Object,
            _logger);

        // Act
        var result = await builder.BuildAsync(shopId, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(50, result.TotalReviews);
        Assert.Equal(4.2m, result.AverageRating);
        Assert.Equal(30, result.FiveStarReviews);
        Assert.Equal(5, result.OneStarReviews);
    }

    [Fact]
    public async Task BuildAsync_Should_Return_Zero_Review_Stats_When_Acl_Returns_Null()
    {
        // Arrange — 防腐层 fail-closed 返回 null，评论统计按零值兜底（非硬编码）
        var shopId = Guid.NewGuid();
        var shop = Shop.Create(shopId, Guid.NewGuid(), "测试店铺", "13800138000");
        var dashboard = ShopDashboardData.Create(shopId);

        _shopRepositoryMock.Setup(r => r.GetByIdAsync(shopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shop);
        _dashboardRepositoryMock.Setup(r => r.GetByShopIdAsync(shopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dashboard);
        _reviewAclMock.Setup(a => a.GetReviewStatisticsAsync(shopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReviewStatisticsDto?)null);

        var builder = new ShopDashboardReadModelBuilder(
            _shopRepositoryMock.Object,
            _dashboardRepositoryMock.Object,
            _reviewAclMock.Object,
            _logger);

        // Act
        var result = await builder.BuildAsync(shopId, CancellationToken.None);

        // Assert — fail-closed 返回 0，但来源是 ?? 兜底而非硬编码
        Assert.NotNull(result);
        Assert.Equal(0, result.TotalReviews);
        Assert.Equal(0m, result.AverageRating);
        Assert.Equal(0, result.FiveStarReviews);
        Assert.Equal(0, result.OneStarReviews);
    }

    [Fact]
    public async Task BuildAsync_Should_Return_Null_When_Shop_Not_Found()
    {
        // Arrange — 店铺不存在，应返回 null
        var shopId = Guid.NewGuid();
        _shopRepositoryMock.Setup(r => r.GetByIdAsync(shopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Shop?)null);

        var builder = new ShopDashboardReadModelBuilder(
            _shopRepositoryMock.Object,
            _dashboardRepositoryMock.Object,
            _reviewAclMock.Object,
            _logger);

        // Act
        var result = await builder.BuildAsync(shopId, CancellationToken.None);

        // Assert
        Assert.Null(result);
        _dashboardRepositoryMock.Verify(r => r.GetByShopIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _reviewAclMock.Verify(a => a.GetReviewStatisticsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task BuildAsync_Should_Return_Null_When_ShopId_Is_Empty()
    {
        // Arrange — ShopId 为空，应返回 null
        var builder = new ShopDashboardReadModelBuilder(
            _shopRepositoryMock.Object,
            _dashboardRepositoryMock.Object,
            _reviewAclMock.Object,
            _logger);

        // Act
        var result = await builder.BuildAsync(Guid.Empty, CancellationToken.None);

        // Assert
        Assert.Null(result);
        _shopRepositoryMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task BuildAsync_Should_Populate_Zero_Orders_When_Dashboard_Is_Null()
    {
        // Arrange — 经营数据尚未建立（无订单事件），dashboard 为 null，订单统计按零值兜底
        var shopId = Guid.NewGuid();
        var shop = Shop.Create(shopId, Guid.NewGuid(), "测试店铺", "13800138000");

        _shopRepositoryMock.Setup(r => r.GetByIdAsync(shopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shop);
        _dashboardRepositoryMock.Setup(r => r.GetByShopIdAsync(shopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShopDashboardData?)null);
        _reviewAclMock.Setup(a => a.GetReviewStatisticsAsync(shopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReviewStatisticsDto { TotalReviews = 10, AverageRating = 4.0m, FiveStarReviews = 5, OneStarReviews = 1 });

        var builder = new ShopDashboardReadModelBuilder(
            _shopRepositoryMock.Object,
            _dashboardRepositoryMock.Object,
            _reviewAclMock.Object,
            _logger);

        // Act
        var result = await builder.BuildAsync(shopId, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.TotalOrders);
        Assert.Equal(0, result.PendingOrders);
        Assert.Equal(0, result.ConfirmedOrders);
        Assert.Equal(0, result.CompletedOrders);
        Assert.Equal(0, result.CancelledOrders);
        Assert.Equal(0m, result.TotalSales);
        // 评论统计仍从 ACL 读取
        Assert.Equal(10, result.TotalReviews);
        Assert.Equal(4.0m, result.AverageRating);
    }

    [Fact]
    public async Task BuildAsync_Should_Set_SchemaVersion_To_Two()
    {
        // Arrange — SchemaVersion 升级为 2，标识读模型包含 ConfirmedOrders/CancelledOrders/评论统计
        var shopId = Guid.NewGuid();
        var shop = Shop.Create(shopId, Guid.NewGuid(), "测试店铺", "13800138000");
        var dashboard = ShopDashboardData.Create(shopId);

        _shopRepositoryMock.Setup(r => r.GetByIdAsync(shopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shop);
        _dashboardRepositoryMock.Setup(r => r.GetByShopIdAsync(shopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dashboard);
        _reviewAclMock.Setup(a => a.GetReviewStatisticsAsync(shopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReviewStatisticsDto());

        var builder = new ShopDashboardReadModelBuilder(
            _shopRepositoryMock.Object,
            _dashboardRepositoryMock.Object,
            _reviewAclMock.Object,
            _logger);

        // Act
        var result = await builder.BuildAsync(shopId, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.SchemaVersion);
    }
}
