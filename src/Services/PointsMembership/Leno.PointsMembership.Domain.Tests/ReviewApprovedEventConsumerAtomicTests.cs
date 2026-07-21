using Leno.Infrastructure.Abstractions;
using Leno.PointsMembership.Domain.Aggregates;
using Leno.PointsMembership.Domain.Repositories;
using Leno.PointsMembership.Infrastructure.Consumers;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;
using System.Reflection;

namespace Leno.PointsMembership.Domain.Tests;

/// <summary>
/// 验证 <see cref="ReviewApprovedEventConsumer"/> 在并发场景下使用 <c>StringIncrementAsync</c> 原子自增，
/// 超限时 <c>StringDecrementAsync</c> 回滚，消除并发突破每日 5 条上限的风险。
/// 关联审计 PM-H06。
/// </summary>
public sealed class ReviewApprovedEventConsumerAtomicTests
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

    [Fact]
    public async Task HandleAsync_Should_Use_StringIncrementAsync_Atomic_Operation()
    {
        var evt = new ReviewApprovedEvent(ReviewId, UserId, Guid.NewGuid(), 5);
        var account = PointsAccount.Create(Guid.NewGuid(), UserId);

        var accountRepoMock = new Mock<IPointsAccountRepository>();
        accountRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var uowMock = new Mock<IUnitOfWork>();
        var loggerMock = new Mock<ILogger<ReviewApprovedEventConsumer>>();
        var idempotencyStoreMock = new Mock<IIdempotencyStore>();
        var redisMock = new Mock<IConnectionMultiplexer>();
        var dbMock = new Mock<IDatabase>();
        dbMock.Setup(d => d.StringIncrementAsync(It.IsAny<RedisKey>(), It.IsAny<long>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(1);
        dbMock.Setup(d => d.KeyExpireAsync(It.IsAny<RedisKey>(), It.IsAny<TimeSpan?>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
        redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object?>())).Returns(dbMock.Object);

        var consumer = new ReviewApprovedEventConsumer(
            accountRepoMock.Object, uowMock.Object, loggerMock.Object, idempotencyStoreMock.Object, redisMock.Object);

        await InvokeHandleAsync(consumer, evt);

        // 验证使用 StringIncrementAsync（原子自增），而非 StringGetAsync + StringSetAsync
        dbMock.Verify(
            d => d.StringIncrementAsync(It.IsAny<RedisKey>(), It.IsAny<long>(), It.IsAny<CommandFlags>()),
            Times.Once);
        dbMock.Verify(
            d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()),
            Times.Never);
        dbMock.Verify(
            d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()),
            Times.Never);
        // 首次自增应设置过期时间
        dbMock.Verify(
            d => d.KeyExpireAsync(It.IsAny<RedisKey>(), It.IsAny<TimeSpan?>(), It.IsAny<CommandFlags>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Should_Decrement_And_Skip_When_Exceed_Limit()
    {
        var evt = new ReviewApprovedEvent(ReviewId, UserId, Guid.NewGuid(), 5);
        var account = PointsAccount.Create(Guid.NewGuid(), UserId);

        var accountRepoMock = new Mock<IPointsAccountRepository>();
        accountRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var uowMock = new Mock<IUnitOfWork>();
        var loggerMock = NullLogger<ReviewApprovedEventConsumer>.Instance;
        var idempotencyStoreMock = new Mock<IIdempotencyStore>();
        var redisMock = new Mock<IConnectionMultiplexer>();
        var dbMock = new Mock<IDatabase>();
        // 自增后返回 6（超过 5 上限）
        dbMock.Setup(d => d.StringIncrementAsync(It.IsAny<RedisKey>(), It.IsAny<long>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(6);
        dbMock.Setup(d => d.StringDecrementAsync(It.IsAny<RedisKey>(), It.IsAny<long>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(5);
        redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object?>())).Returns(dbMock.Object);

        var consumer = new ReviewApprovedEventConsumer(
            accountRepoMock.Object, uowMock.Object, loggerMock, idempotencyStoreMock.Object, redisMock.Object);

        await InvokeHandleAsync(consumer, evt);

        // 应回滚计数
        dbMock.Verify(
            d => d.StringDecrementAsync(It.IsAny<RedisKey>(), It.IsAny<long>(), It.IsAny<CommandFlags>()),
            Times.Once);
        // 不应调用 SaveEntitiesAsync（积分未发放）
        uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
        // 账户余额应保持为 0
        account.Balance.Should().Be(0);
    }

    [Fact]
    public async Task HandleAsync_Should_Decrement_When_Account_NotFound()
    {
        var evt = new ReviewApprovedEvent(ReviewId, UserId, Guid.NewGuid(), 5);

        var accountRepoMock = new Mock<IPointsAccountRepository>();
        accountRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PointsAccount?)null);

        var uowMock = new Mock<IUnitOfWork>();
        var loggerMock = NullLogger<ReviewApprovedEventConsumer>.Instance;
        var idempotencyStoreMock = new Mock<IIdempotencyStore>();
        var redisMock = new Mock<IConnectionMultiplexer>();
        var dbMock = new Mock<IDatabase>();
        // 自增后返回 2（未达上限）
        dbMock.Setup(d => d.StringIncrementAsync(It.IsAny<RedisKey>(), It.IsAny<long>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(2);
        dbMock.Setup(d => d.StringDecrementAsync(It.IsAny<RedisKey>(), It.IsAny<long>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(1);
        redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object?>())).Returns(dbMock.Object);

        var consumer = new ReviewApprovedEventConsumer(
            accountRepoMock.Object, uowMock.Object, loggerMock, idempotencyStoreMock.Object, redisMock.Object);

        await InvokeHandleAsync(consumer, evt);

        // 账户不存在应回滚计数，避免占用当日配额
        dbMock.Verify(
            d => d.StringDecrementAsync(It.IsAny<RedisKey>(), It.IsAny<long>(), It.IsAny<CommandFlags>()),
            Times.Once);
        uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
