using Leno.Infrastructure.Abstractions;
using Leno.PointsMembership.Domain.Aggregates;
using Leno.PointsMembership.Domain.Repositories;
using Leno.PointsMembership.Infrastructure.Consumers;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using System.Reflection;

namespace Leno.PointsMembership.Domain.Tests;

#region ReviewApprovedEventConsumer Tests

public class ReviewApprovedEventConsumerTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid ReviewId = Guid.NewGuid();

    private static async Task InvokeHandleAsync(
        ReviewApprovedEventConsumer consumer,
        ReviewApprovedEvent evt,
        CancellationToken ct = default)
    {
        var method = typeof(ReviewApprovedEventConsumer)
            .GetMethod("HandleAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await (Task)method.Invoke(consumer, [evt, ct])!;
    }

    private static Mock<IDatabase> CreateDatabaseMock(long incrementedCount = 1)
    {
        var dbMock = new Mock<IDatabase>();
        // PM-H06 修复后改用 StringIncrementAsync 原子自增；incrementedCount 表示自增后返回的新值
        dbMock.Setup(d => d.StringIncrementAsync(It.IsAny<RedisKey>(), It.IsAny<long>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(incrementedCount);
        dbMock.Setup(d => d.StringDecrementAsync(It.IsAny<RedisKey>(), It.IsAny<long>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(incrementedCount - 1);
        dbMock.Setup(d => d.KeyExpireAsync(It.IsAny<RedisKey>(), It.IsAny<TimeSpan?>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
        return dbMock;
    }

    [Fact]
    public async Task HandleAsync_ValidEvent_ShouldEarnReviewPoints()
    {
        var evt = new ReviewApprovedEvent(ReviewId, UserId, Guid.NewGuid(), 5);
        var account = PointsAccount.Create(Guid.NewGuid(), UserId);

        var accountRepoMock = new Mock<IPointsAccountRepository>();
        accountRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var uowMock = new Mock<IUnitOfWork>();
        var loggerMock = new Mock<ILogger<ReviewApprovedEventConsumer>>();
        var redisMock = new Mock<IConnectionMultiplexer>();
        // 自增后返回 1（未达上限，正常发放）
        var dbMock = CreateDatabaseMock(incrementedCount: 1);
        redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object?>())).Returns(dbMock.Object);

        var idempotencyStoreMock = new Mock<IIdempotencyStore>();
        var consumer = new ReviewApprovedEventConsumer(
            accountRepoMock.Object, uowMock.Object, loggerMock.Object, idempotencyStoreMock.Object, redisMock.Object);

        await InvokeHandleAsync(consumer, evt);

        account.Balance.Should().Be(10);
        account.TotalEarned.Should().Be(10);
        uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_DailyCapReached_ShouldNotEarnPoints()
    {
        var evt = new ReviewApprovedEvent(ReviewId, UserId, Guid.NewGuid(), 5);
        var account = PointsAccount.Create(Guid.NewGuid(), UserId);

        var accountRepoMock = new Mock<IPointsAccountRepository>();
        accountRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var uowMock = new Mock<IUnitOfWork>();
        var loggerMock = new Mock<ILogger<ReviewApprovedEventConsumer>>();
        var redisMock = new Mock<IConnectionMultiplexer>();
        // 自增后返回 6（超过每日 5 条上限），应回滚并跳过发放
        var dbMock = CreateDatabaseMock(incrementedCount: 6);
        redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object?>())).Returns(dbMock.Object);

        var idempotencyStoreMock = new Mock<IIdempotencyStore>();
        var consumer = new ReviewApprovedEventConsumer(
            accountRepoMock.Object, uowMock.Object, loggerMock.Object, idempotencyStoreMock.Object, redisMock.Object);

        await InvokeHandleAsync(consumer, evt);

        account.Balance.Should().Be(0);
        uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
        // 应回滚计数
        dbMock.Verify(d => d.StringDecrementAsync(It.IsAny<RedisKey>(), It.IsAny<long>(), It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_AccountNotFound_ShouldNotThrow()
    {
        var evt = new ReviewApprovedEvent(ReviewId, UserId, Guid.NewGuid(), 5);

        var accountRepoMock = new Mock<IPointsAccountRepository>();
        accountRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PointsAccount?)null);

        var uowMock = new Mock<IUnitOfWork>();
        var loggerMock = new Mock<ILogger<ReviewApprovedEventConsumer>>();
        var redisMock = new Mock<IConnectionMultiplexer>();
        var dbMock = CreateDatabaseMock(incrementedCount: 1);
        redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object?>())).Returns(dbMock.Object);

        var idempotencyStoreMock = new Mock<IIdempotencyStore>();
        var consumer = new ReviewApprovedEventConsumer(
            accountRepoMock.Object, uowMock.Object, loggerMock.Object, idempotencyStoreMock.Object, redisMock.Object);

        await InvokeHandleAsync(consumer, evt);

        uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}

#endregion
