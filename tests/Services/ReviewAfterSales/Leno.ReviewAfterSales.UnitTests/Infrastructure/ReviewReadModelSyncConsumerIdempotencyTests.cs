using Leno.Infrastructure.Abstractions;
using Leno.Infrastructure.ReadModel;
using Leno.ReviewAfterSales.Domain.Aggregates;
using Leno.ReviewAfterSales.Domain.Repositories;
using Leno.ReviewAfterSales.Infrastructure.ReadModels;
using Leno.SharedContracts.Events;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Leno.ReviewAfterSales.UnitTests.Infrastructure;

/// <summary>
/// 审计 3.6：ReviewReadModelSyncConsumer 未实现 EventId 幂等去重。
/// 验证重复 EventId 投递时 BuildReadModelAsync/IndexAsync 仅被调用一次。
/// 同时验证审计 3.12：Hidden 事件调用 DeleteByIdAsync 删除 ES 文档。
/// </summary>
public sealed class ReviewReadModelSyncConsumerIdempotencyTests
{
    private static Review CreateApprovedReview()
    {
        var review = Review.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), rating: 5, "good", null, sellerId: Guid.NewGuid());
        review.Approve(Guid.NewGuid());
        return review;
    }

    private static ReviewReadModelSyncConsumer CreateConsumer(
        Mock<IReviewRepository> repoMock,
        Mock<IEsReadModelRepository<ReviewReadModel>> esMock,
        Mock<IIdempotencyStore> idempotencyMock)
    {
        return new ReviewReadModelSyncConsumer(
            repoMock.Object,
            esMock.Object,
            NullLogger<ReviewReadModelSyncConsumer>.Instance,
            idempotencyMock.Object);
    }

    private static Mock<IIdempotencyStore> CreateIdempotencyMock(bool isProcessed = false)
    {
        var mock = new Mock<IIdempotencyStore>();
        mock.Setup(s => s.IsProcessedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(isProcessed);
        mock.Setup(s => s.MarkAsProcessedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mock.Setup(s => s.ReleaseProcessingLockAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return mock;
    }

    private static ConsumeContext<T> CreateConsumeContext<T>(T message) where T : class
    {
        var mock = new Mock<ConsumeContext<T>>();
        mock.SetupGet(c => c.Message).Returns(message);
        mock.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return mock.Object;
    }

    [Fact]
    public async Task Consume_Submitted_Should_Skip_When_EventId_Already_Processed()
    {
        var review = CreateApprovedReview();
        var repoMock = new Mock<IReviewRepository>();
        repoMock.Setup(r => r.GetByIdAsync(review.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(review);
        var esMock = new Mock<IEsReadModelRepository<ReviewReadModel>>();
        esMock.Setup(e => e.IndexAsync(It.IsAny<ReviewReadModel>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var idempotencyMock = CreateIdempotencyMock(isProcessed: true);

        var consumer = CreateConsumer(repoMock, esMock, idempotencyMock);
        var evt = new ReviewSubmittedEvent(review.Id, review.UserId, review.SpuId, 5);

        await consumer.Consume(CreateConsumeContext(evt));

        // 已处理的事件应跳过，不调用仓储也不索引
        repoMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        esMock.Verify(e => e.IndexAsync(It.IsAny<ReviewReadModel>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Consume_Submitted_Should_Index_And_Mark_As_Processed()
    {
        var review = CreateApprovedReview();
        var repoMock = new Mock<IReviewRepository>();
        repoMock.Setup(r => r.GetByIdAsync(review.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(review);
        var esMock = new Mock<IEsReadModelRepository<ReviewReadModel>>();
        esMock.Setup(e => e.IndexAsync(It.IsAny<ReviewReadModel>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var idempotencyMock = CreateIdempotencyMock(isProcessed: false);

        var consumer = CreateConsumer(repoMock, esMock, idempotencyMock);
        var evt = new ReviewSubmittedEvent(review.Id, review.UserId, review.SpuId, 5);

        await consumer.Consume(CreateConsumeContext(evt));

        repoMock.Verify(r => r.GetByIdAsync(review.Id, It.IsAny<CancellationToken>()), Times.Once);
        esMock.Verify(e => e.IndexAsync(It.IsAny<ReviewReadModel>(), review.Id.ToString(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        idempotencyMock.Verify(s => s.MarkAsProcessedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Consume_Approved_Should_Skip_When_EventId_Already_Processed()
    {
        var review = CreateApprovedReview();
        var repoMock = new Mock<IReviewRepository>();
        var esMock = new Mock<IEsReadModelRepository<ReviewReadModel>>();
        var idempotencyMock = CreateIdempotencyMock(isProcessed: true);

        var consumer = CreateConsumer(repoMock, esMock, idempotencyMock);
        var evt = new ReviewApprovedEvent(review.Id, review.UserId, review.SpuId, 5);

        await consumer.Consume(CreateConsumeContext(evt));

        repoMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        esMock.Verify(e => e.IndexAsync(It.IsAny<ReviewReadModel>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Consume_Hidden_Should_Call_DeleteByIdAsync_And_Mark_As_Processed()
    {
        var review = CreateApprovedReview();
        var repoMock = new Mock<IReviewRepository>();
        var esMock = new Mock<IEsReadModelRepository<ReviewReadModel>>();
        esMock.Setup(e => e.DeleteByIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var idempotencyMock = CreateIdempotencyMock(isProcessed: false);

        var consumer = CreateConsumer(repoMock, esMock, idempotencyMock);
        var evt = new ReviewHiddenEvent(review.Id, review.SpuId, 5);

        await consumer.Consume(CreateConsumeContext(evt));

        // Hidden 事件应调用 DeleteByIdAsync 删除 ES 文档（审计 3.12）
        esMock.Verify(e => e.DeleteByIdAsync(review.Id.ToString(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        // 不应再 IndexAsync（Hidden 后从搜索结果移除）
        esMock.Verify(e => e.IndexAsync(It.IsAny<ReviewReadModel>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        idempotencyMock.Verify(s => s.MarkAsProcessedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Consume_Submitted_Should_Throw_When_EventId_Empty()
    {
        var repoMock = new Mock<IReviewRepository>();
        var esMock = new Mock<IEsReadModelRepository<ReviewReadModel>>();
        var idempotencyMock = CreateIdempotencyMock(isProcessed: false);

        var consumer = CreateConsumer(repoMock, esMock, idempotencyMock);
        // EventId 为 Guid.Empty（使用反射强制设置或使用 base() 默认值）
        var evt = new ReviewSubmittedEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 5);
        // 通过反射强制 EventId 为 Guid.Empty
        typeof(IntegrationEventBase).GetProperty(nameof(IntegrationEventBase.EventId))!
            .SetValue(evt, Guid.Empty);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            consumer.Consume(CreateConsumeContext(evt)));
    }
}
