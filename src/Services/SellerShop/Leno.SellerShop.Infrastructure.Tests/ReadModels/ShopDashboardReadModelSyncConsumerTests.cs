using Leno.Infrastructure.ReadModel;
using Leno.SellerShop.Infrastructure.ReadModels;
using Leno.SharedContracts.Events;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Leno.SellerShop.Infrastructure.Tests.ReadModels;

/// <summary>
/// 店铺工作台读模型同步消费者单元测试，覆盖 3 个事件触发 IndexAsync 与 builder 返回 null 时跳过同步两类场景。
/// 测试不依赖真实 Elasticsearch 与数据库，全部通过 mock 校验消费者与 builder、ES 仓储的协作。
/// </summary>
public class ShopDashboardReadModelSyncConsumerTests
{
    [Fact]
    public async Task Consume_WhenOrderCreated_ShouldIndexReadModel()
    {
        // Arrange
        var shopId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;

        var readModel = new ShopDashboardReadModel
        {
            ShopId = shopId,
            ShopName = "测试店铺",
            TotalOrders = 1,
            PendingOrders = 1,
            ConfirmedOrders = 0,
            CompletedOrders = 0,
            CancelledOrders = 0,
            TotalReviews = 0,
            AverageRating = 0m,
            FiveStarReviews = 0,
            OneStarReviews = 0,
            TotalSales = 0m,
            Currency = "CNY",
            LastUpdatedAt = createdAt,
            IndexedAt = createdAt,
            SchemaVersion = 1
        };

        var repoMock = new Mock<IEsReadModelRepository<ShopDashboardReadModel>>();
        repoMock.Setup(r => r.IndexAsync(
                It.IsAny<ShopDashboardReadModel>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var builderMock = new Mock<IShopDashboardReadModelBuilder>();
        builderMock.Setup(b => b.BuildAsync(shopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(readModel);

        var consumer = new OrderCreatedShopDashboardSyncConsumer(
            repoMock.Object,
            builderMock.Object,
            NullLogger<OrderCreatedShopDashboardSyncConsumer>.Instance);

        var evt = new OrderCreatedEvent(
            orderId,
            buyerId,
            sellerId: shopId,
            totalAmount: 100m,
            currency: "CNY",
            createdAt,
            sourceCartItemIds: Array.Empty<Guid>());

        // Act
        await consumer.Consume(CreateConsumeContext(evt));

        // Assert：订单创建事件触发 IndexAsync，readModel 字段以 builder 返回为准
        repoMock.Verify(
            r => r.IndexAsync(
                It.Is<ShopDashboardReadModel>(m =>
                    m.ShopId == shopId
                    && m.ShopName == "测试店铺"
                    && m.TotalOrders == 1
                    && m.PendingOrders == 1),
                shopId.ToString(),
                ShopDashboardReadModel.ShopDashboardIndexName,
                It.IsAny<CancellationToken>()),
            Times.Once);
        repoMock.Verify(
            r => r.DeleteByIdAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        builderMock.Verify(b => b.BuildAsync(shopId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Consume_WhenOrderCompleted_ShouldIndexReadModel()
    {
        // Arrange
        var shopId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var completedAt = DateTime.UtcNow;

        var readModel = new ShopDashboardReadModel
        {
            ShopId = shopId,
            ShopName = "测试店铺",
            TotalOrders = 5,
            PendingOrders = 0,
            ConfirmedOrders = 0,
            CompletedOrders = 5,
            CancelledOrders = 0,
            TotalReviews = 0,
            AverageRating = 0m,
            FiveStarReviews = 0,
            OneStarReviews = 0,
            TotalSales = 500m,
            Currency = "CNY",
            LastUpdatedAt = completedAt,
            IndexedAt = completedAt,
            SchemaVersion = 1
        };

        var repoMock = new Mock<IEsReadModelRepository<ShopDashboardReadModel>>();
        repoMock.Setup(r => r.IndexAsync(
                It.IsAny<ShopDashboardReadModel>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var builderMock = new Mock<IShopDashboardReadModelBuilder>();
        builderMock.Setup(b => b.BuildAsync(shopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(readModel);

        var consumer = new OrderCompletedShopDashboardSyncConsumer(
            repoMock.Object,
            builderMock.Object,
            NullLogger<OrderCompletedShopDashboardSyncConsumer>.Instance);

        var evt = new OrderCompletedEvent(
            orderId,
            userId,
            sellerId: shopId,
            totalAmount: 100m,
            currency: "CNY",
            completedAt);

        // Act
        await consumer.Consume(CreateConsumeContext(evt));

        // Assert：订单完成事件触发 IndexAsync，readModel 字段以 builder 返回为准
        repoMock.Verify(
            r => r.IndexAsync(
                It.Is<ShopDashboardReadModel>(m =>
                    m.ShopId == shopId
                    && m.CompletedOrders == 5
                    && m.TotalSales == 500m),
                shopId.ToString(),
                ShopDashboardReadModel.ShopDashboardIndexName,
                It.IsAny<CancellationToken>()),
            Times.Once);
        repoMock.Verify(
            r => r.DeleteByIdAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        builderMock.Verify(b => b.BuildAsync(shopId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Consume_WhenReviewSubmitted_ShouldIndexReadModel()
    {
        // Arrange：ReviewSubmittedEvent 无 ShopId/SellerId 字段，consumer 暂以 SpuId 作为 builder 入参
        var spuId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var readModel = new ShopDashboardReadModel
        {
            ShopId = spuId,
            ShopName = "测试店铺",
            TotalOrders = 0,
            PendingOrders = 0,
            ConfirmedOrders = 0,
            CompletedOrders = 0,
            CancelledOrders = 0,
            TotalReviews = 1,
            AverageRating = 5m,
            FiveStarReviews = 1,
            OneStarReviews = 0,
            TotalSales = 0m,
            Currency = "CNY",
            LastUpdatedAt = DateTime.UtcNow,
            IndexedAt = DateTime.UtcNow,
            SchemaVersion = 1
        };

        var repoMock = new Mock<IEsReadModelRepository<ShopDashboardReadModel>>();
        repoMock.Setup(r => r.IndexAsync(
                It.IsAny<ShopDashboardReadModel>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var builderMock = new Mock<IShopDashboardReadModelBuilder>();
        builderMock.Setup(b => b.BuildAsync(spuId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(readModel);

        var consumer = new ReviewSubmittedShopDashboardSyncConsumer(
            repoMock.Object,
            builderMock.Object,
            NullLogger<ReviewSubmittedShopDashboardSyncConsumer>.Instance);

        var evt = new ReviewSubmittedEvent(reviewId, userId, spuId, rating: 5);

        // Act
        await consumer.Consume(CreateConsumeContext(evt));

        // Assert：评价提交事件触发 IndexAsync，readModel 字段以 builder 返回为准
        repoMock.Verify(
            r => r.IndexAsync(
                It.Is<ShopDashboardReadModel>(m =>
                    m.ShopId == spuId
                    && m.TotalReviews == 1
                    && m.AverageRating == 5m
                    && m.FiveStarReviews == 1),
                spuId.ToString(),
                ShopDashboardReadModel.ShopDashboardIndexName,
                It.IsAny<CancellationToken>()),
            Times.Once);
        repoMock.Verify(
            r => r.DeleteByIdAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        builderMock.Verify(b => b.BuildAsync(spuId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Consume_WhenBuilderReturnsNull_ShouldSkipIndexAndDelete()
    {
        // Arrange：builder 返回 null（店铺不存在），consumer 应跳过索引与删除
        var shopId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();

        var repoMock = new Mock<IEsReadModelRepository<ShopDashboardReadModel>>();
        var builderMock = new Mock<IShopDashboardReadModelBuilder>();
        builderMock.Setup(b => b.BuildAsync(shopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShopDashboardReadModel?)null);

        var consumer = new OrderCreatedShopDashboardSyncConsumer(
            repoMock.Object,
            builderMock.Object,
            NullLogger<OrderCreatedShopDashboardSyncConsumer>.Instance);

        var evt = new OrderCreatedEvent(
            orderId,
            buyerId,
            sellerId: shopId,
            totalAmount: 100m,
            currency: "CNY",
            DateTime.UtcNow,
            sourceCartItemIds: Array.Empty<Guid>());

        // Act
        await consumer.Consume(CreateConsumeContext(evt));

        // Assert：builder 返回 null 时既不索引也不删除
        repoMock.Verify(
            r => r.IndexAsync(
                It.IsAny<ShopDashboardReadModel>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        repoMock.Verify(
            r => r.DeleteByIdAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        builderMock.Verify(b => b.BuildAsync(shopId, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static ConsumeContext<T> CreateConsumeContext<T>(T message) where T : class
    {
        var mock = new Mock<ConsumeContext<T>>();
        mock.Setup(c => c.Message).Returns(message);
        mock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        return mock.Object;
    }
}
