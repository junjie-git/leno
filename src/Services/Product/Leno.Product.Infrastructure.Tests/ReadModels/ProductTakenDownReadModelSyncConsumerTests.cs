using Leno.Infrastructure.ReadModel;
using Leno.Product.Infrastructure.ReadModels;
using Leno.SharedContracts.Events;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Leno.Product.Infrastructure.Tests.ReadModels;

public class ProductTakenDownReadModelSyncConsumerTests
{
    private static readonly Guid ProductId = Guid.NewGuid();
    private static readonly Guid SellerId = Guid.NewGuid();

    [Fact]
    public async Task Consume_WhenProductTakenDown_ShouldCallDeleteByIdAsync()
    {
        // Arrange
        var repoMock = new Mock<IEsReadModelRepository<ProductReadModel>>();
        repoMock.Setup(r => r.DeleteByIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
        var consumer = new ProductTakenDownReadModelSyncConsumer(
            repoMock.Object,
            NullLogger<ProductTakenDownReadModelSyncConsumer>.Instance);
        var evt = new ProductTakenDownEvent(ProductId, SellerId);

        // Act
        await consumer.Consume(CreateConsumeContext(evt));

        // Assert: 调用 DeleteByIdAsync 以 productId 与 leno_products 索引名删除文档
        repoMock.Verify(
            r => r.DeleteByIdAsync(
                ProductId.ToString(),
                ProductSearchService.ProductIndexName,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Consume_WhenDeleteReturnsFalse_ShouldPropagateException()
    {
        // Arrange: 删除失败（DeleteByIdAsync 返回 false）应抛 InvalidOperationException 触发重试
        var repoMock = new Mock<IEsReadModelRepository<ProductReadModel>>();
        repoMock.Setup(r => r.DeleteByIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
        var consumer = new ProductTakenDownReadModelSyncConsumer(
            repoMock.Object,
            NullLogger<ProductTakenDownReadModelSyncConsumer>.Instance);
        var evt = new ProductTakenDownEvent(ProductId, SellerId);

        // Act
        var act = async () => await consumer.Consume(CreateConsumeContext(evt));

        // Assert: 不再仅 LogWarning，必须抛异常以触发 MassTransit 重试与死信队列
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Consume_WhenDeleteThrows_ShouldPropagateException()
    {
        // Arrange: 仓储底层异常应向上传播，触发 MassTransit 重试
        var repoMock = new Mock<IEsReadModelRepository<ProductReadModel>>();
        repoMock.Setup(r => r.DeleteByIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("ES unavailable"));
        var consumer = new ProductTakenDownReadModelSyncConsumer(
            repoMock.Object,
            NullLogger<ProductTakenDownReadModelSyncConsumer>.Instance);
        var evt = new ProductTakenDownEvent(ProductId, SellerId);

        // Act
        var act = async () => await consumer.Consume(CreateConsumeContext(evt));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Consume_WhenProductTakenDown_ShouldNotCallIndexAsync()
    {
        // Arrange: 下架事件仅触发删除分支，不应触发索引分支
        var repoMock = new Mock<IEsReadModelRepository<ProductReadModel>>();
        repoMock.Setup(r => r.DeleteByIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
        var consumer = new ProductTakenDownReadModelSyncConsumer(
            repoMock.Object,
            NullLogger<ProductTakenDownReadModelSyncConsumer>.Instance);
        var evt = new ProductTakenDownEvent(ProductId, SellerId);

        // Act
        await consumer.Consume(CreateConsumeContext(evt));

        // Assert
        repoMock.Verify(
            r => r.IndexAsync(
                It.IsAny<ProductReadModel>(),
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
