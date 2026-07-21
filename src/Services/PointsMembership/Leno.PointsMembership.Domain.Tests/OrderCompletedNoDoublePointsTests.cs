using Leno.Infrastructure.Abstractions;
using Leno.PointsMembership.Domain.Aggregates;
using Leno.PointsMembership.Domain.Repositories;
using Leno.PointsMembership.Infrastructure.Consumers;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Reflection;

namespace Leno.PointsMembership.Domain.Tests;

/// <summary>
/// 验证 PM-H07 修复：<see cref="OrderCompletedEventConsumer"/> 不再发放消费返积分与累加消费金额，
/// 统一由 <see cref="OrderAfterSalesWindowClosedEventConsumer"/> 在售后窗口关闭后发放，消除双倍发放风险。
/// </summary>
public sealed class OrderCompletedNoDoublePointsTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid SellerId = Guid.NewGuid();
    private static readonly Guid AccountId = Guid.NewGuid();
    private static readonly Guid MemberId = Guid.NewGuid();

    private static async Task InvokeHandleAsync<TConsumer, TEvent>(
        TConsumer consumer,
        TEvent evt,
        CancellationToken ct = default)
        where TConsumer : class
    {
        var method = typeof(TConsumer)
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
    public async Task OrderCompletedEventConsumer_Should_Not_Earn_Points_Nor_AddConsumption()
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
            .Returns(Task.CompletedTask);

        var consumer = new OrderCompletedEventConsumer(
            accountRepoMock.Object,
            memberRepoMock.Object,
            levelRepoMock.Object,
            uowMock.Object,
            NullLogger<OrderCompletedEventConsumer>.Instance,
            CreateIdempotencyMock().Object);

        var evt = new OrderCompletedEvent(
            orderId: OrderId,
            userId: UserId,
            sellerId: SellerId,
            totalAmount: 100m,
            currency: "CNY",
            completedAt: DateTime.UtcNow);

        await InvokeHandleAsync(consumer, evt);

        Assert.Equal(0, account.Balance);
        Assert.Equal(0, account.TotalEarned);
        Assert.Equal(0m, member.TotalConsumption);
        uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task OrderAfterSalesWindowClosedEventConsumer_Should_Still_Earn_Points()
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
            .Returns(Task.CompletedTask);

        var consumer = new OrderAfterSalesWindowClosedEventConsumer(
            accountRepoMock.Object,
            memberRepoMock.Object,
            levelRepoMock.Object,
            uowMock.Object,
            NullLogger<OrderAfterSalesWindowClosedEventConsumer>.Instance,
            CreateIdempotencyMock().Object);

        var evt = new OrderAfterSalesWindowClosedEvent(
            OrderId, UserId, paidAmount: 100m, closedAt: DateTime.UtcNow);

        await InvokeHandleAsync(consumer, evt);

        Assert.Equal(100, account.Balance);
        Assert.Equal(100, account.TotalEarned);
        Assert.Equal(100m, member.TotalConsumption);
        uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
