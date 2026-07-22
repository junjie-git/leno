using Leno.PointsMembership.Domain.Events;
using Leno.PointsMembership.Infrastructure.EventBus;

namespace Leno.PointsMembership.Infrastructure.Tests.EventBus;

/// <summary>
/// 验证 <see cref="PointsMembershipIntegrationEventMapper"/> 将领域事件版
/// <see cref="Leno.PointsMembership.Domain.Events.MemberLevelUpgradedEvent"/> 翻译为集成事件版
/// <see cref="Leno.SharedContracts.Events.MemberLevelUpgradedIntegrationEvent"/>（含 MemberId），
/// 供 <c>MemberLevelUpgradedReadModelSyncConsumer</c> 重建 ES 读模型。
/// 关联审计 PM-M05 + D1.3：原映射到 <c>MemberLevelChangedIntegrationEvent</c> 导致读模型同步消费者订阅不到事件；
/// D1.3 将集成事件重命名为 <c>MemberLevelUpgradedIntegrationEvent</c> 消除与领域事件同名混淆。
/// </summary>
public sealed class PointsMembershipIntegrationEventMapperMemberLevelUpgradedTests
{
    [Fact]
    public void Map_DomainMemberLevelUpgradedEvent_Should_Return_IntegrationEventVersion_With_MemberId()
    {
        // Arrange
        var mapper = new PointsMembershipIntegrationEventMapper();
        var memberId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var upgradedAt = DateTime.UtcNow;
        var domainEvent = new Leno.PointsMembership.Domain.Events.MemberLevelUpgradedEvent(
            memberId, userId, oldLevel: 1, newLevel: 3, upgradedAt);

        // Act
        var integrationEvent = mapper.Map(domainEvent);

        // Assert：翻译结果必须是集成事件版 MemberLevelUpgradedIntegrationEvent（非 MemberLevelChangedIntegrationEvent）
        integrationEvent.Should().NotBeNull();
        integrationEvent.Should().BeOfType<Leno.SharedContracts.Events.MemberLevelUpgradedIntegrationEvent>();
        var upgraded = (Leno.SharedContracts.Events.MemberLevelUpgradedIntegrationEvent)integrationEvent!;
        upgraded.MemberId.Should().Be(memberId);
        upgraded.NewLevel.Should().Be(3);
        upgraded.UpgradedAt.Should().Be(upgradedAt);
    }

    [Fact]
    public void Map_DomainMemberLevelUpgradedEvent_Should_Not_Return_MemberLevelChangedIntegrationEvent()
    {
        // Arrange
        var mapper = new PointsMembershipIntegrationEventMapper();
        var memberId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var domainEvent = new Leno.PointsMembership.Domain.Events.MemberLevelUpgradedEvent(
            memberId, userId, oldLevel: 1, newLevel: 2, DateTime.UtcNow);

        // Act
        var integrationEvent = mapper.Map(domainEvent);

        // Assert：不应再发布到 MemberLevelChangedIntegrationEvent（该事件供消息通知域，非读模型同步）
        integrationEvent.Should().NotBeNull();
        integrationEvent.Should().NotBeOfType<Leno.SharedContracts.Events.MemberLevelChangedIntegrationEvent>();
    }

    [Fact]
    public void Map_DomainMemberLevelUpgradedEvent_Should_Preserve_Different_MemberIds()
    {
        // Arrange：多 memberId 轮换验证，避免实现误用单例/共享字段
        var mapper = new PointsMembershipIntegrationEventMapper();
        var memberId1 = Guid.NewGuid();
        var memberId2 = Guid.NewGuid();

        var domainEvent1 = new Leno.PointsMembership.Domain.Events.MemberLevelUpgradedEvent(
            memberId1, Guid.NewGuid(), oldLevel: 1, newLevel: 2, DateTime.UtcNow);
        var domainEvent2 = new Leno.PointsMembership.Domain.Events.MemberLevelUpgradedEvent(
            memberId2, Guid.NewGuid(), oldLevel: 2, newLevel: 4, DateTime.UtcNow);

        // Act
        var evt1 = mapper.Map(domainEvent1);
        var evt2 = mapper.Map(domainEvent2);

        // Assert
        evt1.Should().BeOfType<Leno.SharedContracts.Events.MemberLevelUpgradedIntegrationEvent>();
        evt2.Should().BeOfType<Leno.SharedContracts.Events.MemberLevelUpgradedIntegrationEvent>();
        ((Leno.SharedContracts.Events.MemberLevelUpgradedIntegrationEvent)evt1!).MemberId.Should().Be(memberId1);
        ((Leno.SharedContracts.Events.MemberLevelUpgradedIntegrationEvent)evt2!).MemberId.Should().Be(memberId2);
    }
}
