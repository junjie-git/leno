using Leno.Infrastructure.ReadModel;
using Leno.SellerShop.Application.Services;
using Leno.SellerShop.Infrastructure.ReadModels;
using Leno.SharedContracts.Events;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Leno.SellerShop.Infrastructure.Tests.ReadModels;

/// <summary>
/// ReviewSubmittedShopDashboardSyncConsumer 单元测试。
/// 验证消费者优先读取 ReviewSubmittedEvent.ShopId 字段；
/// ShopId 为空时通过 IProductAntiCorruptionService 反查 SPU 归属卖家；
/// 反查返回 null 时跳过同步；
/// 未注入防腐层时退回旧行为（以 SpuId 作为 builder 入参）。
/// </summary>
public sealed class ReviewSubmittedShopDashboardSyncConsumerTests
{
    private readonly Mock<IEsReadModelRepository<ShopDashboardReadModel>> _repositoryMock = new();
    private readonly Mock<IShopDashboardReadModelBuilder> _builderMock = new();
    private readonly Mock<IProductAntiCorruptionService> _productAclMock = new();

    [Fact]
    public async Task BuildReadModelAsync_Should_Use_ShopId_From_Event_When_Provided()
    {
        // Arrange — 事件携带 ShopId，应直接使用，不调用防腐层
        var shopId = Guid.NewGuid();
        var spuId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        var integrationEvent = new ReviewSubmittedEvent
        {
            ReviewId = reviewId,
            SpuId = spuId,
            ShopId = shopId,
            Rating = 5,
            NewScore = 4.5,
            ReviewCount = 10
        };
        var expectedReadModel = new ShopDashboardReadModel { ShopId = shopId };
        _builderMock
            .Setup(b => b.BuildAsync(shopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedReadModel);

        var consumer = CreateConsumer();

        // Act
        var result = await InvokeBuildReadModelAsync(consumer, integrationEvent);

        // Assert
        Assert.Equal(shopId.ToString(), result.Id);
        Assert.Equal(ShopDashboardReadModel.ShopDashboardIndexName, result.IndexName);
        Assert.NotNull(result.ReadModel);
        _builderMock.Verify(b => b.BuildAsync(shopId, It.IsAny<CancellationToken>()), Times.Once);
        _productAclMock.Verify(a => a.GetSpuSellerIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task BuildReadModelAsync_Should_Fallback_To_Acl_When_ShopId_Is_Empty()
    {
        // Arrange — 事件未携带 ShopId，通过防腐层反查 SPU 归属卖家
        var spuId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var integrationEvent = new ReviewSubmittedEvent
        {
            ReviewId = Guid.NewGuid(),
            SpuId = spuId,
            ShopId = Guid.Empty,
            Rating = 5,
            NewScore = 4.5,
            ReviewCount = 10
        };
        var expectedReadModel = new ShopDashboardReadModel { ShopId = sellerId };
        _productAclMock
            .Setup(a => a.GetSpuSellerIdAsync(spuId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sellerId);
        _builderMock
            .Setup(b => b.BuildAsync(sellerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedReadModel);

        var consumer = CreateConsumer();

        // Act
        var result = await InvokeBuildReadModelAsync(consumer, integrationEvent);

        // Assert
        Assert.Equal(sellerId.ToString(), result.Id);
        Assert.NotNull(result.ReadModel);
        _productAclMock.Verify(a => a.GetSpuSellerIdAsync(spuId, It.IsAny<CancellationToken>()), Times.Once);
        _builderMock.Verify(b => b.BuildAsync(sellerId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BuildReadModelAsync_Should_Return_Empty_When_ShopId_Empty_And_Acl_Returns_Null()
    {
        // Arrange — 防腐层反查返回 null（SPU 不存在或跨域故障），应跳过同步
        var spuId = Guid.NewGuid();
        var integrationEvent = new ReviewSubmittedEvent
        {
            ReviewId = Guid.NewGuid(),
            SpuId = spuId,
            ShopId = Guid.Empty,
            Rating = 5,
            NewScore = 4.5,
            ReviewCount = 10
        };
        _productAclMock
            .Setup(a => a.GetSpuSellerIdAsync(spuId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);

        var consumer = CreateConsumer();

        // Act
        var result = await InvokeBuildReadModelAsync(consumer, integrationEvent);

        // Assert
        Assert.Equal(string.Empty, result.Id);
        Assert.Null(result.ReadModel);
        _builderMock.Verify(b => b.BuildAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task BuildReadModelAsync_Should_Fallback_To_SpuId_When_Acl_Not_Injected()
    {
        // Arrange — 兼容路径：未注入防腐层（3 参数构造函数），ShopId 为空时退回旧行为：以 SpuId 作为 builder 入参
        var spuId = Guid.NewGuid();
        var integrationEvent = new ReviewSubmittedEvent
        {
            ReviewId = Guid.NewGuid(),
            SpuId = spuId,
            ShopId = Guid.Empty,
            Rating = 5
        };
        var expectedReadModel = new ShopDashboardReadModel { ShopId = spuId };
        _builderMock
            .Setup(b => b.BuildAsync(spuId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedReadModel);

        // 使用 3 参数构造函数（不注入防腐层）
        var consumer = new ReviewSubmittedShopDashboardSyncConsumer(
            _repositoryMock.Object,
            _builderMock.Object,
            NullLogger<ReviewSubmittedShopDashboardSyncConsumer>.Instance);

        // Act
        var result = await InvokeBuildReadModelAsync(consumer, integrationEvent);

        // Assert
        Assert.Equal(spuId.ToString(), result.Id);
        Assert.NotNull(result.ReadModel);
        _builderMock.Verify(b => b.BuildAsync(spuId, It.IsAny<CancellationToken>()), Times.Once);
        _productAclMock.Verify(a => a.GetSpuSellerIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private ReviewSubmittedShopDashboardSyncConsumer CreateConsumer()
    {
        return new ReviewSubmittedShopDashboardSyncConsumer(
            _repositoryMock.Object,
            _builderMock.Object,
            _productAclMock.Object,
            NullLogger<ReviewSubmittedShopDashboardSyncConsumer>.Instance);
    }

    private static async Task<(string Id, string IndexName, ShopDashboardReadModel? ReadModel)> InvokeBuildReadModelAsync(
        ReviewSubmittedShopDashboardSyncConsumer consumer, ReviewSubmittedEvent integrationEvent)
    {
        var method = typeof(ReviewSubmittedShopDashboardSyncConsumer)
            .GetMethod("BuildReadModelAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (method is null)
        {
            throw new InvalidOperationException("BuildReadModelAsync 方法未找到");
        }

        var task = (Task<(string Id, string IndexName, ShopDashboardReadModel? ReadModel)>)method.Invoke(
            consumer, new object[] { integrationEvent, CancellationToken.None })!;
        return await task;
    }
}
