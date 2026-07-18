using Leno.Infrastructure.ReadModel;
using Leno.Promotion.Infrastructure.ReadModels;
using Leno.SharedContracts.Events;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Leno.Promotion.Infrastructure.Tests.ReadModels;

public class CouponReadModelSyncConsumerTests
{
    [Fact]
    public async Task Consume_WhenCouponCreated_ShouldIndexReadModel()
    {
        // Arrange
        var repoMock = new Mock<IEsReadModelRepository<CouponReadModel>>();
        repoMock.Setup(r => r.IndexAsync(
                It.IsAny<CouponReadModel>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var consumer = new CouponCreatedReadModelSyncConsumer(
            repoMock.Object,
            NullLogger<CouponCreatedReadModelSyncConsumer>.Instance);

        var couponId = Guid.NewGuid();
        var evt = new CouponCreatedEvent(
            couponId,
            name: "满 100 减 20",
            couponType: "FullReduction",
            faceValue: 20m,
            minSpend: 100m,
            validFrom: DateTime.UtcNow,
            validTo: DateTime.UtcNow.AddDays(30),
            totalQty: 1000,
            status: "Enabled");

        // Act
        await consumer.Consume(CreateConsumeContext(evt));

        // Assert
        repoMock.Verify(
            r => r.IndexAsync(
                It.Is<CouponReadModel>(m =>
                    m.CouponId == couponId
                    && m.Name == "满 100 减 20"
                    && m.CouponType == "FullReduction"
                    && m.FaceValue == 20m
                    && m.MinSpend == 100m
                    && m.TotalQty == 1000
                    && m.IssuedQty == 0
                    && m.Status == "Enabled"),
                couponId.ToString(),
                CouponReadModel.CouponIndexName,
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
    public async Task Consume_WhenCouponDisabled_ShouldDeleteReadModel()
    {
        // Arrange
        var repoMock = new Mock<IEsReadModelRepository<CouponReadModel>>();
        repoMock.Setup(r => r.DeleteByIdAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var consumer = new CouponDisabledReadModelSyncConsumer(
            repoMock.Object,
            NullLogger<CouponDisabledReadModelSyncConsumer>.Instance);

        var couponId = Guid.NewGuid();
        var evt = new CouponDisabledEvent(couponId, DateTime.UtcNow);

        // Act
        await consumer.Consume(CreateConsumeContext(evt));

        // Assert: 券模板停用删除读模型（避免用户端检索到已停用券模板）
        repoMock.Verify(
            r => r.DeleteByIdAsync(
                couponId.ToString(),
                CouponReadModel.CouponIndexName,
                It.IsAny<CancellationToken>()),
            Times.Once);
        repoMock.Verify(
            r => r.IndexAsync(
                It.IsAny<CouponReadModel>(),
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
