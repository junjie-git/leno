using System.Reflection;
using Leno.Infrastructure.Abstractions;
using Leno.Infrastructure.ReadModel;
using Leno.Product.Infrastructure.ReadModels;
using Leno.SharedContracts.Events;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Leno.Product.Infrastructure.Tests.ReadModels;

/// <summary>
/// P1-T9 单元测试：验证 <see cref="SpuReviewSubmittedSummaryConsumer"/> 与
/// <see cref="SpuReviewHiddenSummaryConsumer"/> 使用 TotalScore 累计值消除浮点漂移。
/// 原实现每次增量 Math.Round(Score, 2) 回写，千次评价后漂移 ±0.05；
/// 修复后维护原始累计 TotalScore，展示时 Score = Round(TotalScore / ReviewCount, 2)。
/// 通过反射调用受保护的 HandleAsync，使用 Mock 仓储返回共享读模型实例模拟增量。
/// </summary>
public class SpuReviewSummaryConsumerTests
{
    /// <summary>
    /// 模拟 1000 次评价提交（评分 1-5 随机），增量更新后 Score 应与全量重算一致（无浮点漂移）。
    /// </summary>
    [Fact]
    public async Task Submitted_ThousandIncrements_ScoreMatchesFullRecalculation()
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

        var (consumer, _) = CreateSubmittedConsumer(model);

        // Act — 1000 次评价提交，评分 1-5 随机（固定种子可重现）
        var random = new Random(42);
        var ratings = new List<int>();
        for (var i = 0; i < 1000; i++)
        {
            var rating = random.Next(1, 6);
            ratings.Add(rating);
            var evt = new ReviewSubmittedEvent
            {
                EventId = Guid.NewGuid(),
                SpuId = spuId,
                ReviewId = Guid.NewGuid(),
                Rating = rating
            };
            await InvokeHandleAsync(consumer, evt);
        }

        // Assert
        var expectedTotalScore = ratings.Sum(x => (double)x);
        var expectedScore = Math.Round(expectedTotalScore / ratings.Count, 2);

        model.ReviewCount.Should().Be(1000);
        model.TotalScore.Should().Be(expectedTotalScore, "TotalScore 应为所有评分的精确累计值");
        model.Score.Should().Be(expectedScore, "1000 次增量后 Score 应与全量重算一致（无浮点漂移）");
    }

    /// <summary>
    /// 提交 3 条评价后隐藏 1 条，Score 应等于剩余 2 条的全量重算值。
    /// </summary>
    [Fact]
    public async Task Hidden_AfterSubmitThenHide_ScoreMatchesRemainingRatings()
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

        var (submittedConsumer, _) = CreateSubmittedConsumer(model);
        var (hiddenConsumer, _) = CreateHiddenConsumer(model);

        // Act — 提交 3 条评价（评分 5, 4, 3）
        var submittedRatings = new[] { 5, 4, 3 };
        var reviewIds = new List<Guid>();
        foreach (var rating in submittedRatings)
        {
            var reviewId = Guid.NewGuid();
            reviewIds.Add(reviewId);
            var submitEvt = new ReviewSubmittedEvent
            {
                EventId = Guid.NewGuid(),
                SpuId = spuId,
                ReviewId = reviewId,
                Rating = rating
            };
            await InvokeHandleAsync(submittedConsumer, submitEvt);
        }

        // 隐藏第 2 条评价（评分 4）
        var hiddenEvt = new ReviewHiddenEvent
        {
            EventId = Guid.NewGuid(),
            SpuId = spuId,
            ReviewId = reviewIds[1],
            Rating = 4
        };
        await InvokeHandleAsync(hiddenConsumer, hiddenEvt);

        // Assert — 剩余 2 条评分（5, 3）的全量重算
        var remainingRatings = new[] { 5, 3 };
        var expectedTotalScore = remainingRatings.Sum(x => (double)x);
        var expectedScore = Math.Round(expectedTotalScore / remainingRatings.Length, 2);

        model.ReviewCount.Should().Be(2);
        model.TotalScore.Should().Be(expectedTotalScore);
        model.Score.Should().Be(expectedScore);
    }

    /// <summary>
    /// 仅 1 条评价时隐藏，Score/TotalScore/ReviewCount 均归零。
    /// </summary>
    [Fact]
    public async Task Hidden_LastReview_ScoreAndTotalScoreResetToZero()
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

        var (submittedConsumer, _) = CreateSubmittedConsumer(model);
        var (hiddenConsumer, _) = CreateHiddenConsumer(model);

        // 提交 1 条评分 5
        await InvokeHandleAsync(submittedConsumer, new ReviewSubmittedEvent
        {
            EventId = Guid.NewGuid(),
            SpuId = spuId,
            ReviewId = Guid.NewGuid(),
            Rating = 5
        });

        model.ReviewCount.Should().Be(1);
        model.Score.Should().Be(5.0);

        // Act — 隐藏最后一条
        await InvokeHandleAsync(hiddenConsumer, new ReviewHiddenEvent
        {
            EventId = Guid.NewGuid(),
            SpuId = spuId,
            ReviewId = Guid.NewGuid(),
            Rating = 5
        });

        // Assert
        model.ReviewCount.Should().Be(0);
        model.TotalScore.Should().Be(0);
        model.Score.Should().Be(0);
    }

    /// <summary>
    /// 提交+隐藏交替 500 轮（每轮提交 2 条隐藏 1 条），最终 Score 应与净累计一致。
    /// </summary>
    [Fact]
    public async Task Mixed_SubmitAndHideAlternating_ScoreMatchesNetTotal()
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

        var (submittedConsumer, _) = CreateSubmittedConsumer(model);
        var (hiddenConsumer, _) = CreateHiddenConsumer(model);

        // Act — 500 轮：每轮提交 2 条（评分 4, 5）后隐藏 1 条（评分 4）
        // 净效果：每轮净增 1 条评分 5
        for (var i = 0; i < 500; i++)
        {
            await InvokeHandleAsync(submittedConsumer, new ReviewSubmittedEvent
            {
                EventId = Guid.NewGuid(),
                SpuId = spuId,
                ReviewId = Guid.NewGuid(),
                Rating = 4
            });
            await InvokeHandleAsync(submittedConsumer, new ReviewSubmittedEvent
            {
                EventId = Guid.NewGuid(),
                SpuId = spuId,
                ReviewId = Guid.NewGuid(),
                Rating = 5
            });
            await InvokeHandleAsync(hiddenConsumer, new ReviewHiddenEvent
            {
                EventId = Guid.NewGuid(),
                SpuId = spuId,
                ReviewId = Guid.NewGuid(),
                Rating = 4
            });
        }

        // Assert — 500 轮后净 500 条评分 5
        model.ReviewCount.Should().Be(500);
        model.TotalScore.Should().Be(500 * 5.0);
        model.Score.Should().Be(5.0);
    }

    /// <summary>
    /// 构造 SpuReviewSubmittedSummaryConsumer，Mock 仓储返回共享读模型实例。
    /// </summary>
    private static (SpuReviewSubmittedSummaryConsumer Consumer, Mock<IEsReadModelRepository<ProductReadModel>> MockRepo)
        CreateSubmittedConsumer(ProductReadModel model)
    {
        var mockRepo = CreateMockRepo(model);
        var consumer = new SpuReviewSubmittedSummaryConsumer(
            mockRepo.Object,
            NullLogger<SpuReviewSubmittedSummaryConsumer>.Instance,
            new Mock<IIdempotencyStore>().Object);
        return (consumer, mockRepo);
    }

    /// <summary>
    /// 构造 SpuReviewHiddenSummaryConsumer，Mock 仓储返回共享读模型实例。
    /// </summary>
    private static (SpuReviewHiddenSummaryConsumer Consumer, Mock<IEsReadModelRepository<ProductReadModel>> MockRepo)
        CreateHiddenConsumer(ProductReadModel model)
    {
        var mockRepo = CreateMockRepo(model);
        var consumer = new SpuReviewHiddenSummaryConsumer(
            mockRepo.Object,
            NullLogger<SpuReviewHiddenSummaryConsumer>.Instance,
            new Mock<IIdempotencyStore>().Object);
        return (consumer, mockRepo);
    }

    /// <summary>
    /// 创建 Mock 仓储：GetByIdAsync 返回共享读模型实例，IndexAsync 返回 true。
    /// </summary>
    private static Mock<IEsReadModelRepository<ProductReadModel>> CreateMockRepo(ProductReadModel model)
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
        return mockRepo;
    }

    /// <summary>
    /// 通过反射调用受保护的 HandleAsync 方法。
    /// </summary>
    private static async Task InvokeHandleAsync(object consumer, object integrationEvent)
    {
        var handleMethod = consumer.GetType()
            .GetMethod("HandleAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        handleMethod.Should().NotBeNull("HandleAsync 应为受保护的虚方法");
        await (Task)handleMethod!.Invoke(consumer, [integrationEvent, CancellationToken.None])!;
    }
}
