using Leno.Infrastructure.ReadModel;
using Leno.Promotion.Infrastructure.ReadModels;
using Leno.SharedContracts.Events;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Leno.Promotion.Infrastructure.Tests.ReadModels;

public class SeckillActivityReadModelSyncConsumerTests
{
    [Fact]
    public async Task Consume_WhenActivityPublished_ShouldIndexReadModel()
    {
        // Arrange
        var repoMock = new Mock<IEsReadModelRepository<SeckillActivityReadModel>>();
        repoMock.Setup(r => r.IndexAsync(
                It.IsAny<SeckillActivityReadModel>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var consumer = new SeckillActivityPublishedReadModelSyncConsumer(
            repoMock.Object,
            NullLogger<SeckillActivityPublishedReadModelSyncConsumer>.Instance);

        var activityId = Guid.NewGuid();
        var evt = new SeckillActivityPublishedEvent(
            activityId,
            spuId: Guid.NewGuid(),
            skuId: Guid.NewGuid(),
            seckillPrice: 10m,
            originalPrice: 100m,
            totalStock: 1000,
            startTime: DateTime.UtcNow,
            endTime: DateTime.UtcNow.AddHours(2),
            status: "Active");

        // Act
        await consumer.Consume(CreateConsumeContext(evt));

        // Assert
        repoMock.Verify(
            r => r.IndexAsync(
                It.Is<SeckillActivityReadModel>(m =>
                    m.ActivityId == activityId
                    && m.SeckillPrice == 10m
                    && m.OriginalPrice == 100m
                    && m.TotalStock == 1000
                    && m.AvailableStock == 1000
                    && m.Status == "Active"),
                activityId.ToString(),
                SeckillActivityReadModel.SeckillActivityIndexName,
                It.IsAny<CancellationToken>()),
            Times.Once);
        repoMock.Verify(
            r => r.DeleteByIdAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Consume_WhenActivityEnded_ShouldDeleteReadModel()
    {
        // Arrange
        var repoMock = new Mock<IEsReadModelRepository<SeckillActivityReadModel>>();
        repoMock.Setup(r => r.DeleteByIdAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var consumer = new SeckillActivityEndedReadModelSyncConsumer(
            repoMock.Object,
            NullLogger<SeckillActivityEndedReadModelSyncConsumer>.Instance);

        var activityId = Guid.NewGuid();
        var evt = new SeckillActivityEndedEvent(activityId, DateTime.UtcNow, "SoldOut");

        // Act
        await consumer.Consume(CreateConsumeContext(evt));

        // Assert: 活动结束删除读模型（避免前台展示已结束活动）
        repoMock.Verify(
            r => r.DeleteByIdAsync(
                activityId.ToString(),
                SeckillActivityReadModel.SeckillActivityIndexName,
                It.IsAny<CancellationToken>()),
            Times.Once);
        repoMock.Verify(
            r => r.IndexAsync(
                It.IsAny<SeckillActivityReadModel>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static ConsumeContext<T> CreateConsumeContext<T>(T message) where T : class
    {
        var mock = new Mock<ConsumeContext<T>>();
        mock.Setup(c => c.Message).Returns(message);
        mock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        return mock.Object;
    }
}
