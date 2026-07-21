using Leno.ReviewAfterSales.Application;
using Leno.ReviewAfterSales.Application.InternalQueryServices;
using Leno.ReviewAfterSales.Domain.Repositories;
using Leno.ReviewAfterSales.Domain.ValueObjects;
using Moq;
using Xunit;

namespace Leno.ReviewAfterSales.UnitTests.Application;

/// <summary>
/// 审计 3.4：ReviewInternalQueryService.GetProductRatingAsync 加载全部 Approved 评价到内存计算聚合。
/// 验证改造为 SQL 聚合后，仅调用仓储 GetRatingSnapshotAsync 一次，不再加载全部评价到内存。
/// </summary>
public sealed class ReviewInternalQueryServiceRatingTests
{
    [Fact]
    public async Task GetProductRatingAsync_Should_Use_Sql_Snapshot_Not_Memory_Aggregation()
    {
        var spuId = Guid.NewGuid();
        var repoMock = new Mock<IReviewRepository>();
        repoMock.Setup(r => r.GetRatingSnapshotAsync(spuId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProductRatingSnapshot
            {
                SpuId = spuId,
                AverageRating = 4.5,
                TotalCount = 100,
                PositiveCount = 80
            });
        // 验证不再调用 GetBySpuIdAsync 加载全部评价到内存
        repoMock.Setup(r => r.GetBySpuIdAsync(It.IsAny<Guid>(), It.IsAny<ReviewStatus?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Domain.Aggregates.Review>());

        var svc = new ReviewInternalQueryService(repoMock.Object);

        var dto = await svc.GetProductRatingAsync(spuId);

        Assert.NotNull(dto);
        Assert.Equal(spuId, dto!.SpuId);
        Assert.Equal(4.5, dto.AverageRating);
        Assert.Equal(100, dto.TotalCount);
        Assert.Equal(80, dto.PositiveCount);

        // 必须调用仓储 GetRatingSnapshotAsync
        repoMock.Verify(r => r.GetRatingSnapshotAsync(spuId, It.IsAny<CancellationToken>()), Times.Once);
        // 不应调用 GetBySpuIdAsync 加载全部评价到内存
        repoMock.Verify(r => r.GetBySpuIdAsync(It.IsAny<Guid>(), It.IsAny<ReviewStatus?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetProductRatingAsync_Should_Return_Null_When_Snapshot_Null()
    {
        var spuId = Guid.NewGuid();
        var repoMock = new Mock<IReviewRepository>();
        repoMock.Setup(r => r.GetRatingSnapshotAsync(spuId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductRatingSnapshot?)null);

        var svc = new ReviewInternalQueryService(repoMock.Object);

        var dto = await svc.GetProductRatingAsync(spuId);

        Assert.Null(dto);
    }
}
