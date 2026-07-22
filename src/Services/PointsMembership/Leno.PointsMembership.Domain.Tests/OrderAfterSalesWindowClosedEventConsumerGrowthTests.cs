using Leno.Infrastructure.Abstractions;
using Leno.PointsMembership.Domain.Aggregates;
using Leno.PointsMembership.Domain.Repositories;
using Leno.PointsMembership.Domain.ValueObjects;
using Leno.PointsMembership.Infrastructure.Consumers;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Reflection;

namespace Leno.PointsMembership.Domain.Tests;

/// <summary>
/// 验证 PM-H01 修复：<see cref="OrderAfterSalesWindowClosedEventConsumer"/> 在发放消费返积分时
/// 同步累加 <see cref="Member.AddGrowthValue"/>（1 积分 = 1 成长值），打通 V0-V4 成长值等级体系。
/// </summary>
public sealed class OrderAfterSalesWindowClosedEventConsumerGrowthTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid AccountId = Guid.NewGuid();
    private static readonly Guid MemberId = Guid.NewGuid();

    private static async Task InvokeHandleAsync(
        OrderAfterSalesWindowClosedEventConsumer consumer,
        OrderAfterSalesWindowClosedEvent evt,
        CancellationToken ct = default)
    {
        var method = typeof(OrderAfterSalesWindowClosedEventConsumer)
            .GetMethod("HandleAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await (Task)method.Invoke(consumer, [evt, ct])!;
    }

    private static Mock<IIdempotencyStore> CreateIdempotencyMock()
    {
        var mock = new Mock<IIdempotencyStore>();
        mock.Setup(s => s.IsProcessedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        mock.Setup(s => s.MarkAsProcessedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mock.Setup(s => s.TryMarkAsProcessingAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        mock.SetupGet(s => s.SupportsAtomicProcessing).Returns(false);
        return mock;
    }

    [Fact]
    public async Task HandleAsync_Should_Accumulate_GrowthValue_Equal_To_Points()
    {
        var account = PointsAccount.Create(AccountId, UserId);
        var member = Member.Create(MemberId, UserId);

        var accountRepoMock = new Mock<IPointsAccountRepository>();
        accountRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var memberRepoMock = new Mock<IMemberRepository>();
        memberRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);

        var levelRepoMock = new Mock<IMembershipLevelRepository>();
        levelRepoMock.Setup(r => r.GetAllEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MembershipLevel>());

        var uowMock = new Mock<IUnitOfWork>();
        uowMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var consumer = new OrderAfterSalesWindowClosedEventConsumer(
            accountRepoMock.Object,
            memberRepoMock.Object,
            levelRepoMock.Object,
            uowMock.Object,
            NullLogger<OrderAfterSalesWindowClosedEventConsumer>.Instance,
            CreateIdempotencyMock().Object);

        var evt = new OrderAfterSalesWindowClosedEvent(
            OrderId, UserId, paidAmount: 80m, closedAt: DateTime.UtcNow);

        await InvokeHandleAsync(consumer, evt);

        // 消费返 80 积分，应同步累加 80 成长值
        Assert.Equal(80, account.Balance);
        Assert.Equal(80, member.GrowthValue);
    }

    [Fact]
    public async Task HandleAsync_Should_Not_Accumulate_GrowthValue_When_Points_Zero()
    {
        var member = Member.Create(MemberId, UserId);

        var memberRepoMock = new Mock<IMemberRepository>();
        memberRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);

        var levelRepoMock = new Mock<IMembershipLevelRepository>();
        levelRepoMock.Setup(r => r.GetAllEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MembershipLevel>());

        var uowMock = new Mock<IUnitOfWork>();
        uowMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var consumer = new OrderAfterSalesWindowClosedEventConsumer(
            new Mock<IPointsAccountRepository>().Object,
            memberRepoMock.Object,
            levelRepoMock.Object,
            uowMock.Object,
            NullLogger<OrderAfterSalesWindowClosedEventConsumer>.Instance,
            CreateIdempotencyMock().Object);

        // 0.4 元 → floor = 0 积分，不发放积分也不累加成长值
        var evt = new OrderAfterSalesWindowClosedEvent(
            OrderId, UserId, paidAmount: 0.4m, closedAt: DateTime.UtcNow);

        await InvokeHandleAsync(consumer, evt);

        Assert.Equal(0, member.GrowthValue);
    }

    [Fact]
    public async Task HandleAsync_Should_Skip_GrowthValue_When_Member_NotFound()
    {
        var account = PointsAccount.Create(AccountId, UserId);

        var accountRepoMock = new Mock<IPointsAccountRepository>();
        accountRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var memberRepoMock = new Mock<IMemberRepository>();
        memberRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Member?)null);

        var levelRepoMock = new Mock<IMembershipLevelRepository>();
        levelRepoMock.Setup(r => r.GetAllEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MembershipLevel>());

        var uowMock = new Mock<IUnitOfWork>();
        uowMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var consumer = new OrderAfterSalesWindowClosedEventConsumer(
            accountRepoMock.Object,
            memberRepoMock.Object,
            levelRepoMock.Object,
            uowMock.Object,
            NullLogger<OrderAfterSalesWindowClosedEventConsumer>.Instance,
            CreateIdempotencyMock().Object);

        var evt = new OrderAfterSalesWindowClosedEvent(
            OrderId, UserId, paidAmount: 100m, closedAt: DateTime.UtcNow);

        // 不应抛异常（member null 时跳过 AddGrowthValue）
        await InvokeHandleAsync(consumer, evt);

        // 积分仍应发放（account 与 member 解耦）
        Assert.Equal(100, account.Balance);
    }
}
