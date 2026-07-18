using Leno.Infrastructure.ReadModel;
using Leno.PointsMembership.Domain.Aggregates;
using Leno.PointsMembership.Domain.Repositories;
using Leno.PointsMembership.Infrastructure.ReadModels;
using Leno.SharedContracts.Events;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Leno.PointsMembership.Infrastructure.Tests.ReadModels;

public class PointsAccountReadModelSyncConsumerTests
{
    [Fact]
    public async Task Consume_WhenPointsAccountCreated_ShouldIndexReadModel()
    {
        // Arrange
        var repoMock = new Mock<IEsReadModelRepository<PointsAccountReadModel>>();
        repoMock.Setup(r => r.IndexAsync(
                It.IsAny<PointsAccountReadModel>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var consumer = new PointsAccountCreatedReadModelSyncConsumer(
            repoMock.Object,
            NullLogger<PointsAccountCreatedReadModelSyncConsumer>.Instance);

        var accountId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;
        var evt = new PointsAccountCreatedEvent(accountId, userId, initialPoints: 100, createdAt);

        // Act
        await consumer.Consume(CreateConsumeContext(evt));

        // Assert: 账户创建事件触发 IndexAsync（初始余额与累计获取等于 InitialPoints）
        repoMock.Verify(
            r => r.IndexAsync(
                It.Is<PointsAccountReadModel>(m =>
                    m.PointsAccountId == accountId
                    && m.UserId == userId
                    && m.Balance == 100
                    && m.FrozenAmount == 0
                    && m.TotalEarned == 100
                    && m.TotalSpent == 0
                    && m.LastAdjustedAt == null
                    && m.Status == "Active"),
                accountId.ToString(),
                PointsAccountReadModel.PointsAccountIndexName,
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
    public async Task Consume_WhenPointsAdjusted_ShouldRebuildReadModelFromRepository()
    {
        // Arrange：模拟仓储返回最新聚合根快照（Balance 已扣减）
        var accountId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var account = PointsAccount.Create(accountId, userId);
        account.Earn(Leno.PointsMembership.Domain.ValueObjects.PointsSource.NewUser, 200, "新人注册奖励");
        account.ConsumePoints(50, Guid.NewGuid(), "兑换优惠券");

        var repoMock = new Mock<IEsReadModelRepository<PointsAccountReadModel>>();
        repoMock.Setup(r => r.IndexAsync(
                It.IsAny<PointsAccountReadModel>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var accountRepoMock = new Mock<IPointsAccountRepository>();
        accountRepoMock.Setup(r => r.GetByIdAsync(accountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var consumer = new PointsAdjustedReadModelSyncConsumer(
            repoMock.Object,
            accountRepoMock.Object,
            NullLogger<PointsAdjustedReadModelSyncConsumer>.Instance);

        var adjustedAt = DateTime.UtcNow;
        var evt = new PointsAdjustedEvent(accountId, delta: -50, reason: "兑换优惠券", adjustedAt);

        // Act
        await consumer.Consume(CreateConsumeContext(evt));

        // Assert：余额变更事件触发 IndexAsync，readModel 字段以聚合根最新状态为准
        repoMock.Verify(
            r => r.IndexAsync(
                It.Is<PointsAccountReadModel>(m =>
                    m.PointsAccountId == accountId
                    && m.UserId == userId
                    && m.Balance == account.Balance
                    && m.FrozenAmount == account.FrozenBalance
                    && m.TotalEarned == account.TotalEarned
                    && m.TotalSpent == account.TotalSpent
                    && m.LastAdjustedAt == adjustedAt),
                accountId.ToString(),
                PointsAccountReadModel.PointsAccountIndexName,
                It.IsAny<CancellationToken>()),
            Times.Once);
        repoMock.Verify(
            r => r.DeleteByIdAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        accountRepoMock.Verify(r => r.GetByIdAsync(accountId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Consume_WhenPointsAdjustedAndAccountMissing_ShouldSkipIndexAndDelete()
    {
        // Arrange：仓储返回 null（账户已删除），consumer 应跳过索引与删除
        var accountId = Guid.NewGuid();
        var repoMock = new Mock<IEsReadModelRepository<PointsAccountReadModel>>();
        var accountRepoMock = new Mock<IPointsAccountRepository>();
        accountRepoMock.Setup(r => r.GetByIdAsync(accountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PointsAccount?)null);

        var consumer = new PointsAdjustedReadModelSyncConsumer(
            repoMock.Object,
            accountRepoMock.Object,
            NullLogger<PointsAdjustedReadModelSyncConsumer>.Instance);

        var evt = new PointsAdjustedEvent(accountId, delta: -50, reason: "兑换优惠券", DateTime.UtcNow);

        // Act
        await consumer.Consume(CreateConsumeContext(evt));

        // Assert：聚合根不存在时既不索引也不删除
        repoMock.Verify(
            r => r.IndexAsync(
                It.IsAny<PointsAccountReadModel>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        repoMock.Verify(
            r => r.DeleteByIdAsync(
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
