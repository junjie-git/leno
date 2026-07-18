using Leno.Infrastructure.ReadModel;
using Leno.PointsMembership.Domain.Aggregates;
using Leno.PointsMembership.Domain.Repositories;
using Leno.PointsMembership.Infrastructure.ReadModels;
using Leno.SharedContracts.Events;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Leno.PointsMembership.Infrastructure.Tests.ReadModels;

public class MemberReadModelSyncConsumerTests
{
    [Fact]
    public async Task Consume_WhenMemberRegistered_ShouldIndexReadModel()
    {
        // Arrange
        var repoMock = new Mock<IEsReadModelRepository<MemberReadModel>>();
        repoMock.Setup(r => r.IndexAsync(
                It.IsAny<MemberReadModel>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var consumer = new MemberRegisteredReadModelSyncConsumer(
            repoMock.Object,
            NullLogger<MemberRegisteredReadModelSyncConsumer>.Instance);

        var memberId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var registeredAt = DateTime.UtcNow;
        var evt = new MemberRegisteredEvent(memberId, userId, level: 1, registeredAt);

        // Act
        await consumer.Consume(CreateConsumeContext(evt));

        // Assert：会员档案创建事件触发 IndexAsync
        repoMock.Verify(
            r => r.IndexAsync(
                It.Is<MemberReadModel>(m =>
                    m.MemberId == memberId
                    && m.UserId == userId
                    && m.Level == 1
                    && m.TotalConsumption == 0
                    && m.GrowthValue == 0
                    && m.GrowthLevel == 0
                    && m.RegisteredAt == registeredAt
                    && m.LastUpgradeAt == registeredAt
                    && m.Status == "Active"),
                memberId.ToString(),
                MemberReadModel.MemberIndexName,
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
    public async Task Consume_WhenMemberLevelUpgraded_ShouldRebuildReadModelFromRepository()
    {
        // Arrange：模拟仓储返回最新会员聚合根（等级已升级）
        var memberId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var member = Member.Create(memberId, userId);
        // 直接调用 AddConsumption + CheckUpgrade 触发等级升级需要 LevelThreshold 集合；
        // 这里仅校验 consumer 读取聚合根快照重建读模型，不依赖具体等级变化。

        var repoMock = new Mock<IEsReadModelRepository<MemberReadModel>>();
        repoMock.Setup(r => r.IndexAsync(
                It.IsAny<MemberReadModel>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var memberRepoMock = new Mock<IMemberRepository>();
        memberRepoMock.Setup(r => r.GetByIdAsync(memberId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);

        var consumer = new MemberLevelUpgradedReadModelSyncConsumer(
            repoMock.Object,
            memberRepoMock.Object,
            NullLogger<MemberLevelUpgradedReadModelSyncConsumer>.Instance);

        var upgradedAt = DateTime.UtcNow;
        var evt = new MemberLevelUpgradedEvent(memberId, newLevel: 3, upgradedAt);

        // Act
        await consumer.Consume(CreateConsumeContext(evt));

        // Assert：等级升级事件触发 IndexAsync，readModel 字段以聚合根最新状态为准
        repoMock.Verify(
            r => r.IndexAsync(
                It.Is<MemberReadModel>(m =>
                    m.MemberId == memberId
                    && m.UserId == userId
                    && m.Level == member.CurrentLevel
                    && m.TotalConsumption == member.TotalConsumption
                    && m.GrowthValue == member.GrowthValue
                    && m.GrowthLevel == member.CurrentGrowthLevel
                    && m.RegisteredAt == member.JoinedAt
                    && m.LastUpgradeAt == member.LevelUpgradedAt
                    && m.Status == member.Status.ToString()),
                memberId.ToString(),
                MemberReadModel.MemberIndexName,
                It.IsAny<CancellationToken>()),
            Times.Once);
        repoMock.Verify(
            r => r.DeleteByIdAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        memberRepoMock.Verify(r => r.GetByIdAsync(memberId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Consume_WhenMemberLevelUpgradedAndMemberMissing_ShouldSkipIndexAndDelete()
    {
        // Arrange：仓储返回 null（会员已删除），consumer 应跳过索引与删除
        var memberId = Guid.NewGuid();
        var repoMock = new Mock<IEsReadModelRepository<MemberReadModel>>();
        var memberRepoMock = new Mock<IMemberRepository>();
        memberRepoMock.Setup(r => r.GetByIdAsync(memberId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Member?)null);

        var consumer = new MemberLevelUpgradedReadModelSyncConsumer(
            repoMock.Object,
            memberRepoMock.Object,
            NullLogger<MemberLevelUpgradedReadModelSyncConsumer>.Instance);

        var evt = new MemberLevelUpgradedEvent(memberId, newLevel: 3, DateTime.UtcNow);

        // Act
        await consumer.Consume(CreateConsumeContext(evt));

        // Assert：聚合根不存在时既不索引也不删除
        repoMock.Verify(
            r => r.IndexAsync(
                It.IsAny<MemberReadModel>(),
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
