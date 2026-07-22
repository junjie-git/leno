using System.Reflection;
using Leno.Infrastructure.Abstractions;
using Leno.Infrastructure.ReadModel;
using Leno.Product.Infrastructure.ReadModels;
using Leno.SharedContracts.Events;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Leno.Product.Infrastructure.Tests.ReadModels;

/// <summary>
/// P1-T10 单元测试：验证 <see cref="SpuReviewModeratedSummaryConsumer"/> 根据
/// 审核动作（approve/reject/hide/appeal）正确增量更新评分摘要。
/// 修复审计 #10：原实现仅有 TODO 占位，审核驳回后商品评分读模型仍包含被驳回评价。
/// </summary>
public class SpuReviewModeratedSummaryConsumerTests
{
    /// <summary>
    /// approve 动作应将评分计入摘要：TotalScore += Rating、ReviewCount += 1。
    /// </summary>
    [Fact]
    public async Task HandleAsync_ApproveAction_AddsRatingToSummary()
    {
        // Arrange
        var spuId = Guid.NewGuid();
        var model = new ProductReadModel
        {
            Id = spuId,
            Title = "Test Product",
            Score = 4.0,
            TotalScore = 8.0,
            ReviewCount = 2
        };

        var (consumer, _) = CreateConsumer(model);

        // Act — 审核通过评分 5
        await InvokeHandleAsync(consumer, new ReviewModeratedEvent
        {
            EventId = Guid.NewGuid(),
            ReviewId = Guid.NewGuid(),
            SpuId = spuId,
            Rating = 5,
            Action = "approve",
            Status = 1
        });

        // Assert — 8 + 5 = 13, 2 + 1 = 3, Score = Round(13/3, 2) = 4.33
        model.TotalScore.Should().Be(13.0);
        model.ReviewCount.Should().Be(3);
        model.Score.Should().Be(Math.Round(13.0 / 3, 2));
    }

    /// <summary>
    /// appeal 动作应将评分重新计入摘要：TotalScore += Rating、ReviewCount += 1。
    /// </summary>
    [Fact]
    public async Task HandleAsync_AppealAction_AddsRatingToSummary()
    {
        // Arrange
        var spuId = Guid.NewGuid();
        var model = new ProductReadModel
        {
            Id = spuId,
            Title = "Test Product",
            Score = 4.0,
            TotalScore = 4.0,
            ReviewCount = 1
        };

        var (consumer, _) = CreateConsumer(model);

        // Act — 申诉恢复评分 3
        await InvokeHandleAsync(consumer, new ReviewModeratedEvent
        {
            EventId = Guid.NewGuid(),
            ReviewId = Guid.NewGuid(),
            SpuId = spuId,
            Rating = 3,
            Action = "appeal",
            Status = 1
        });

        // Assert — 4 + 3 = 7, 1 + 1 = 2, Score = Round(7/2, 2) = 3.5
        model.TotalScore.Should().Be(7.0);
        model.ReviewCount.Should().Be(2);
        model.Score.Should().Be(Math.Round(7.0 / 2, 2));
    }

    /// <summary>
    /// reject 动作应从摘要移除评分：TotalScore -= Rating、ReviewCount -= 1。
    /// </summary>
    [Fact]
    public async Task HandleAsync_RejectAction_RemovesRatingFromSummary()
    {
        // Arrange
        var spuId = Guid.NewGuid();
        var model = new ProductReadModel
        {
            Id = spuId,
            Title = "Test Product",
            Score = 4.0,
            TotalScore = 12.0,
            ReviewCount = 3
        };

        var (consumer, _) = CreateConsumer(model);

        // Act — 驳回评分 5
        await InvokeHandleAsync(consumer, new ReviewModeratedEvent
        {
            EventId = Guid.NewGuid(),
            ReviewId = Guid.NewGuid(),
            SpuId = spuId,
            Rating = 5,
            Action = "reject",
            Status = 2
        });

        // Assert — 12 - 5 = 7, 3 - 1 = 2, Score = Round(7/2, 2) = 3.5
        model.TotalScore.Should().Be(7.0);
        model.ReviewCount.Should().Be(2);
        model.Score.Should().Be(Math.Round(7.0 / 2, 2));
    }

    /// <summary>
    /// hide 动作应从摘要移除评分（与 reject 等价）。
    /// </summary>
    [Fact]
    public async Task HandleAsync_HideAction_RemovesRatingFromSummary()
    {
        // Arrange
        var spuId = Guid.NewGuid();
        var model = new ProductReadModel
        {
            Id = spuId,
            Title = "Test Product",
            Score = 5.0,
            TotalScore = 10.0,
            ReviewCount = 2
        };

        var (consumer, _) = CreateConsumer(model);

        // Act — 隐藏评分 4
        await InvokeHandleAsync(consumer, new ReviewModeratedEvent
        {
            EventId = Guid.NewGuid(),
            ReviewId = Guid.NewGuid(),
            SpuId = spuId,
            Rating = 4,
            Action = "hide",
            Status = 2
        });

        // Assert — 10 - 4 = 6, 2 - 1 = 1, Score = Round(6/1, 2) = 6.0
        model.TotalScore.Should().Be(6.0);
        model.ReviewCount.Should().Be(1);
        model.Score.Should().Be(6.0);
    }

    /// <summary>
    /// reject 动作在仅剩 1 条评价时应归零 Score/TotalScore/ReviewCount。
    /// </summary>
    [Fact]
    public async Task HandleAsync_RejectLastReview_ResetsToZero()
    {
        // Arrange
        var spuId = Guid.NewGuid();
        var model = new ProductReadModel
        {
            Id = spuId,
            Title = "Test Product",
            Score = 5.0,
            TotalScore = 5.0,
            ReviewCount = 1
        };

        var (consumer, _) = CreateConsumer(model);

        // Act
        await InvokeHandleAsync(consumer, new ReviewModeratedEvent
        {
            EventId = Guid.NewGuid(),
            ReviewId = Guid.NewGuid(),
            SpuId = spuId,
            Rating = 5,
            Action = "reject",
            Status = 2
        });

        // Assert
        model.TotalScore.Should().Be(0);
        model.ReviewCount.Should().Be(0);
        model.Score.Should().Be(0);
    }

    /// <summary>
    /// reject 动作在 ReviewCount=0 时应跳过（不产生负数）。
    /// </summary>
    [Fact]
    public async Task HandleAsync_RejectWhenCountZero_SkipsUpdate()
    {
        // Arrange
        var spuId = Guid.NewGuid();
        var model = new ProductReadModel
        {
            Id = spuId,
            Title = "Test Product",
            Score = 0,
            TotalScore = 0,
            ReviewCount = 0
        };

        var (consumer, mockRepo) = CreateConsumer(model);

        // Act
        await InvokeHandleAsync(consumer, new ReviewModeratedEvent
        {
            EventId = Guid.NewGuid(),
            ReviewId = Guid.NewGuid(),
            SpuId = spuId,
            Rating = 3,
            Action = "reject",
            Status = 2
        });

        // Assert — 状态不变，IndexAsync 不应被调用
        model.TotalScore.Should().Be(0);
        model.ReviewCount.Should().Be(0);
        model.Score.Should().Be(0);
        mockRepo.Verify(r => r.IndexAsync(
            It.IsAny<ProductReadModel>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// 缺少 SpuId（Guid.Empty）时应跳过更新。
    /// </summary>
    [Fact]
    public async Task HandleAsync_MissingSpuId_SkipsUpdate()
    {
        // Arrange
        var model = new ProductReadModel
        {
            Id = Guid.NewGuid(),
            Title = "Test Product",
            Score = 4.0,
            TotalScore = 8.0,
            ReviewCount = 2
        };

        var (consumer, mockRepo) = CreateConsumer(model);

        // Act
        await InvokeHandleAsync(consumer, new ReviewModeratedEvent
        {
            EventId = Guid.NewGuid(),
            ReviewId = Guid.NewGuid(),
            SpuId = Guid.Empty,
            Rating = 5,
            Action = "approve",
            Status = 1
        });

        // Assert — 状态不变
        model.TotalScore.Should().Be(8.0);
        model.ReviewCount.Should().Be(2);
        mockRepo.Verify(r => r.IndexAsync(
            It.IsAny<ProductReadModel>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// 未知动作应跳过更新。
    /// </summary>
    [Fact]
    public async Task HandleAsync_UnknownAction_SkipsUpdate()
    {
        // Arrange
        var spuId = Guid.NewGuid();
        var model = new ProductReadModel
        {
            Id = spuId,
            Title = "Test Product",
            Score = 4.0,
            TotalScore = 8.0,
            ReviewCount = 2
        };

        var (consumer, mockRepo) = CreateConsumer(model);

        // Act
        await InvokeHandleAsync(consumer, new ReviewModeratedEvent
        {
            EventId = Guid.NewGuid(),
            ReviewId = Guid.NewGuid(),
            SpuId = spuId,
            Rating = 5,
            Action = "unknown_action",
            Status = 1
        });

        // Assert — 状态不变
        model.TotalScore.Should().Be(8.0);
        model.ReviewCount.Should().Be(2);
        mockRepo.Verify(r => r.IndexAsync(
            It.IsAny<ProductReadModel>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// 读模型不存在时应跳过更新。
    /// </summary>
    [Fact]
    public async Task HandleAsync_ReadModelNotFound_SkipsUpdate()
    {
        // Arrange
        var spuId = Guid.NewGuid();
        var mockRepo = new Mock<IEsReadModelRepository<ProductReadModel>>();
        mockRepo.Setup(r => r.GetByIdAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductReadModel?)null);

        var consumer = new SpuReviewModeratedSummaryConsumer(
            mockRepo.Object,
            NullLogger<SpuReviewModeratedSummaryConsumer>.Instance,
            new Mock<IIdempotencyStore>().Object);

        // Act
        await InvokeHandleAsync(consumer, new ReviewModeratedEvent
        {
            EventId = Guid.NewGuid(),
            ReviewId = Guid.NewGuid(),
            SpuId = spuId,
            Rating = 5,
            Action = "approve",
            Status = 1
        });

        // Assert — IndexAsync 不应被调用
        mockRepo.Verify(r => r.IndexAsync(
            It.IsAny<ProductReadModel>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// 构造 SpuReviewModeratedSummaryConsumer，Mock 仓储返回共享读模型实例。
    /// </summary>
    private static (SpuReviewModeratedSummaryConsumer Consumer, Mock<IEsReadModelRepository<ProductReadModel>> MockRepo)
        CreateConsumer(ProductReadModel model)
    {
        var mockRepo = new Mock<IEsReadModelRepository<ProductReadModel>>();
        mockRepo.Setup(r => r.GetByIdAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(model);
        mockRepo.Setup(r => r.IndexAsync(
                It.IsAny<ProductReadModel>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var consumer = new SpuReviewModeratedSummaryConsumer(
            mockRepo.Object,
            NullLogger<SpuReviewModeratedSummaryConsumer>.Instance,
            new Mock<IIdempotencyStore>().Object);
        return (consumer, mockRepo);
    }

    /// <summary>
    /// 通过反射调用受保护的 HandleAsync 方法。
    /// </summary>
    private static async Task InvokeHandleAsync(SpuReviewModeratedSummaryConsumer consumer, ReviewModeratedEvent integrationEvent)
    {
        var handleMethod = consumer.GetType()
            .GetMethod("HandleAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        handleMethod.Should().NotBeNull("HandleAsync 应为受保护的虚方法");
        await (Task)handleMethod!.Invoke(consumer, [integrationEvent, CancellationToken.None])!;
    }
}
