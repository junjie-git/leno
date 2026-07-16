using Leno.Order.Domain.Aggregates;
using Leno.Order.Domain.Exceptions;
using Leno.Order.Domain.Repositories;
using Leno.Order.Infrastructure.Services;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Leno.Order.Infrastructure.Tests;

/// <summary>
/// 库存预占回滚补偿（T18）单元测试。
/// 覆盖：
/// - <see cref="StockReservationCompensation"/> 聚合状态流转（Create/MarkFailed/MarkSucceeded/MaxRetries）
/// - <see cref="StockReservationCompensationBackgroundService.RunRetryCycleAsync"/> 成功/失败/空批/混合场景
/// 测试风格参考 <see cref="InventoryReconciliationBackgroundServiceTests"/>（Moq + FluentAssertions + xUnit）。
/// </summary>
public sealed class StockReservationCompensationTests
{
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid SkuId = Guid.NewGuid();

    #region 聚合根 StockReservationCompensation

    [Fact]
    public void Create_Valid_ShouldBePendingWithZeroRetries()
    {
        var compensation = StockReservationCompensation.Create(Guid.NewGuid(), OrderId, SkuId, 5);

        compensation.OrderId.Should().Be(OrderId);
        compensation.SkuId.Should().Be(SkuId);
        compensation.Quantity.Should().Be(5);
        compensation.Status.Should().Be(CompensationStatus.Pending);
        compensation.RetryCount.Should().Be(0);
        compensation.MaxRetries.Should().Be(StockReservationCompensation.DefaultMaxRetries);
        compensation.LastAttemptedAt.Should().BeNull();
        compensation.LastErrorMessage.Should().BeNull();
    }

    [Fact]
    public void Create_InvalidArguments_ShouldThrow()
    {
        var act1 = () => StockReservationCompensation.Create(Guid.NewGuid(), Guid.Empty, SkuId, 5);
        act1.Should().Throw<OrderDomainException>().Which.ErrorCode.Should().Be("STOCK_COMPENSATION_ORDER_EMPTY");

        var act2 = () => StockReservationCompensation.Create(Guid.NewGuid(), OrderId, Guid.Empty, 5);
        act2.Should().Throw<OrderDomainException>().Which.ErrorCode.Should().Be("STOCK_COMPENSATION_SKU_EMPTY");

        var act3 = () => StockReservationCompensation.Create(Guid.NewGuid(), OrderId, SkuId, 0);
        act3.Should().Throw<OrderDomainException>().Which.ErrorCode.Should().Be("STOCK_COMPENSATION_QTY_INVALID");
    }

    [Fact]
    public void MarkFailed_ShouldIncrementRetryCountAndStayPending()
    {
        var compensation = StockReservationCompensation.Create(Guid.NewGuid(), OrderId, SkuId, 5);

        compensation.MarkFailed("timeout");

        compensation.RetryCount.Should().Be(1);
        compensation.Status.Should().Be(CompensationStatus.Pending);
        compensation.LastAttemptedAt.Should().NotBeNull();
        compensation.LastErrorMessage.Should().Be("timeout");
    }

    [Fact]
    public void MarkFailed_ReachingMaxRetries_ShouldTransitionToMaxRetriesExceeded()
    {
        var compensation = StockReservationCompensation.Create(Guid.NewGuid(), OrderId, SkuId, 5, maxRetries: 2);

        compensation.MarkFailed("err1");
        compensation.Status.Should().Be(CompensationStatus.Pending);

        compensation.MarkFailed("err2");
        compensation.Status.Should().Be(CompensationStatus.MaxRetriesExceeded);
        compensation.RetryCount.Should().Be(2);
    }

    [Fact]
    public void MarkSucceeded_ShouldTransitionToSucceeded()
    {
        var compensation = StockReservationCompensation.Create(Guid.NewGuid(), OrderId, SkuId, 5);

        compensation.MarkSucceeded();

        compensation.Status.Should().Be(CompensationStatus.Succeeded);
        compensation.LastAttemptedAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkSucceeded_AfterSucceeded_ShouldBeIdempotent()
    {
        var compensation = StockReservationCompensation.Create(Guid.NewGuid(), OrderId, SkuId, 5);
        compensation.MarkSucceeded();
        var firstAttemptedAt = compensation.LastAttemptedAt;

        compensation.MarkSucceeded();

        compensation.Status.Should().Be(CompensationStatus.Succeeded);
        compensation.LastAttemptedAt.Should().Be(firstAttemptedAt);
    }

    [Fact]
    public void MarkFailed_AfterSucceeded_ShouldBeNoop()
    {
        var compensation = StockReservationCompensation.Create(Guid.NewGuid(), OrderId, SkuId, 5);
        compensation.MarkSucceeded();

        compensation.MarkFailed("late error");

        compensation.Status.Should().Be(CompensationStatus.Succeeded);
        compensation.RetryCount.Should().Be(0);
    }

    [Fact]
    public void MarkFailed_LongErrorMessage_ShouldBeTruncated()
    {
        var compensation = StockReservationCompensation.Create(Guid.NewGuid(), OrderId, SkuId, 5);
        var longMessage = new string('x', 600);

        compensation.MarkFailed(longMessage);

        compensation.LastErrorMessage.Should().HaveLength(500);
    }

    #endregion

    #region BackgroundService RunRetryCycleAsync

    [Fact]
    public async Task RunRetryCycleAsync_NoPending_ShouldNotCallRelease()
    {
        var sut = CreateSut(out var compensationRepoMock, out var inventoryRepoMock, out var uowMock);
        compensationRepoMock
            .Setup(r => r.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StockReservationCompensation>());

        await sut.RunRetryCycleAsync(CancellationToken.None);

        inventoryRepoMock.Verify(
            r => r.ReleaseAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
        uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunRetryCycleAsync_ReleaseSucceeds_ShouldMarkSucceededAndPersist()
    {
        var sut = CreateSut(out var compensationRepoMock, out var inventoryRepoMock, out var uowMock);
        var compensation = StockReservationCompensation.Create(Guid.NewGuid(), OrderId, SkuId, 5);
        compensationRepoMock
            .Setup(r => r.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StockReservationCompensation> { compensation });

        await sut.RunRetryCycleAsync(CancellationToken.None);

        inventoryRepoMock.Verify(
            r => r.ReleaseAsync(SkuId, OrderId, 5, It.IsAny<CancellationToken>()),
            Times.Once);
        compensation.Status.Should().Be(CompensationStatus.Succeeded);
        compensationRepoMock.Verify(
            r => r.UpdateAsync(It.Is<StockReservationCompensation>(c => c.Status == CompensationStatus.Succeeded), It.IsAny<CancellationToken>()),
            Times.Once);
        uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunRetryCycleAsync_ReleaseFails_ShouldMarkFailedAndPersist()
    {
        var sut = CreateSut(out var compensationRepoMock, out var inventoryRepoMock, out var uowMock);
        var compensation = StockReservationCompensation.Create(Guid.NewGuid(), OrderId, SkuId, 5);
        compensationRepoMock
            .Setup(r => r.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StockReservationCompensation> { compensation });
        inventoryRepoMock
            .Setup(r => r.ReleaseAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("redis down"));

        await sut.RunRetryCycleAsync(CancellationToken.None);

        compensation.Status.Should().Be(CompensationStatus.Pending);
        compensation.RetryCount.Should().Be(1);
        compensation.LastErrorMessage.Should().Be("redis down");
        compensationRepoMock.Verify(
            r => r.UpdateAsync(It.Is<StockReservationCompensation>(c => c.RetryCount == 1), It.IsAny<CancellationToken>()),
            Times.Once);
        uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunRetryCycleAsync_MixedBatch_ShouldProcessAllIndependently()
    {
        var sut = CreateSut(out var compensationRepoMock, out var inventoryRepoMock, out var uowMock);
        var ok = StockReservationCompensation.Create(Guid.NewGuid(), OrderId, SkuId, 3);
        var bad = StockReservationCompensation.Create(Guid.NewGuid(), OrderId, SkuId, 4);
        compensationRepoMock
            .Setup(r => r.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StockReservationCompensation> { ok, bad });
        inventoryRepoMock
            .Setup(r => r.ReleaseAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), 3, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        inventoryRepoMock
            .Setup(r => r.ReleaseAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), 4, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("fail"));

        await sut.RunRetryCycleAsync(CancellationToken.None);

        ok.Status.Should().Be(CompensationStatus.Succeeded);
        bad.Status.Should().Be(CompensationStatus.Pending);
        bad.RetryCount.Should().Be(1);
        inventoryRepoMock.Verify(
            r => r.ReleaseAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        compensationRepoMock.Verify(
            r => r.UpdateAsync(It.IsAny<StockReservationCompensation>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task RunRetryCycleAsync_ReleaseFailsAndPersistsFails_ShouldNotThrowAndContinue()
    {
        var sut = CreateSut(out var compensationRepoMock, out var inventoryRepoMock, out var uowMock);
        var compensation = StockReservationCompensation.Create(Guid.NewGuid(), OrderId, SkuId, 5);
        compensationRepoMock
            .Setup(r => r.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StockReservationCompensation> { compensation });
        inventoryRepoMock
            .Setup(r => r.ReleaseAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("release fail"));
        uowMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db down"));

        // 持久化失败不应抛出，应被捕获并记日志，不影响后续
        var act = () => sut.RunRetryCycleAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
        compensation.MarkFailed("should still apply in-memory");
    }

    #endregion

    /// <summary>
    /// 构造 BackgroundService 测试桩，返回三个 mock 供断言。
    /// 使用真实 ServiceCollection 构建 ServiceProvider，避免手写 IServiceProvider mock。
    /// </summary>
    private static StockReservationCompensationBackgroundService CreateSut(
        out Mock<IStockReservationCompensationRepository> compensationRepoMock,
        out Mock<IInventoryRepository> inventoryRepoMock,
        out Mock<IUnitOfWork> uowMock)
    {
        compensationRepoMock = new Mock<IStockReservationCompensationRepository>();
        inventoryRepoMock = new Mock<IInventoryRepository>();
        uowMock = new Mock<IUnitOfWork>();

        var services = new ServiceCollection();
        services.AddSingleton(compensationRepoMock.Object);
        services.AddSingleton(inventoryRepoMock.Object);
        services.AddSingleton(uowMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        var options = Options.Create(new StockReservationCompensationOptions
        {
            Interval = TimeSpan.FromHours(1),
            BatchSize = 10
        });
        var logger = new Mock<ILogger<StockReservationCompensationBackgroundService>>().Object;

        return new StockReservationCompensationBackgroundService(serviceProvider, logger, options);
    }
}
