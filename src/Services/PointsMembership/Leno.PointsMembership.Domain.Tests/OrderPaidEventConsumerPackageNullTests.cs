using FluentAssertions;
using Leno.Infrastructure.Abstractions;
using Leno.PointsMembership.Domain.Aggregates;
using Leno.PointsMembership.Domain.Exceptions;
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
/// 验证 PM-H08 修复：<see cref="OrderPaidEventConsumer"/> 在 package 为 null 或 DurationDays&lt;=0 时
/// 记录告警并跳过 <see cref="UserMembership.Activate"/>，<see cref="UserMembership.Activate"/> 增加 OrderId 幂等检查，
/// 消除消息重试死循环与重复激活抛异常问题。
/// </summary>
public sealed class OrderPaidEventConsumerPackageNullTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid SellerId = Guid.NewGuid();
    private static readonly Guid PaymentId = Guid.NewGuid();
    private static readonly Guid AccountId = Guid.NewGuid();
    private static readonly Guid UserMembershipId = Guid.NewGuid();
    private static readonly Guid PackageId = Guid.NewGuid();

    private static async Task InvokeHandleAsync(
        OrderPaidEventConsumer consumer,
        OrderPaidEvent evt,
        CancellationToken ct = default)
    {
        var method = typeof(OrderPaidEventConsumer)
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

    private static OrderPaidEvent CreateOrderPaidEvent(decimal amount)
        => new(
            orderId: OrderId,
            userId: UserId,
            sellerId: SellerId,
            paymentId: PaymentId,
            channel: "Alipay",
            paidAt: DateTime.UtcNow,
            tradeNo: "T12345",
            amount: amount,
            currency: "CNY");

    [Fact]
    public async Task HandleAsync_Should_Skip_Activate_And_Not_Throw_When_Package_Is_Null()
    {
        var account = PointsAccount.Create(AccountId, UserId);
        account.Earn(PointsSource.Activity, 200, "种子积分");
        account.Freeze(100, OrderId);
        var userMembership = UserMembership.Create(UserMembershipId, UserId, PackageId, level: 1);

        var accountRepoMock = new Mock<IPointsAccountRepository>();
        accountRepoMock.Setup(r => r.GetByFrozenOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var userMembershipRepoMock = new Mock<IUserMembershipRepository>();
        userMembershipRepoMock.Setup(r => r.GetByOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userMembership);

        var packageRepoMock = new Mock<IMembershipPackageRepository>();
        packageRepoMock.Setup(r => r.GetByIdAsync(PackageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MembershipPackage?)null);

        var uowMock = new Mock<IUnitOfWork>();
        uowMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var consumer = new OrderPaidEventConsumer(
            accountRepoMock.Object,
            userMembershipRepoMock.Object,
            packageRepoMock.Object,
            uowMock.Object,
            NullLogger<OrderPaidEventConsumer>.Instance,
            CreateIdempotencyMock().Object);

        var evt = CreateOrderPaidEvent(amount: 100m);

        // 不应抛异常
        await InvokeHandleAsync(consumer, evt);

        // ConfirmDeduct 仍应执行
        Assert.Equal(100, account.TotalSpent);
        // UserMembership 不应被激活
        Assert.Equal(UserMembershipStatus.Pending, userMembership.Status);
        uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Should_Skip_Activate_When_Package_DurationDays_NotPositive()
    {
        // 构造 DurationDays 异常的 package 较难（Create 会校验），通过 mock 直接返回构造好的对象绕过校验
        var package = MembershipPackage.Create(
            PackageId, name: "异常套餐", level: 1, price: 30m, durationDays: 30, benefits: "测试");
        // 通过反射将 DurationDays 置为 0 模拟数据异常
        typeof(MembershipPackage)
            .GetProperty(nameof(MembershipPackage.DurationDays))!
            .SetValue(package, 0);

        var account = PointsAccount.Create(AccountId, UserId);
        account.Earn(PointsSource.Activity, 200, "种子积分");
        account.Freeze(100, OrderId);
        var userMembership = UserMembership.Create(UserMembershipId, UserId, PackageId, level: 1);

        var accountRepoMock = new Mock<IPointsAccountRepository>();
        accountRepoMock.Setup(r => r.GetByFrozenOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var userMembershipRepoMock = new Mock<IUserMembershipRepository>();
        userMembershipRepoMock.Setup(r => r.GetByOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userMembership);

        var packageRepoMock = new Mock<IMembershipPackageRepository>();
        packageRepoMock.Setup(r => r.GetByIdAsync(PackageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(package);

        var uowMock = new Mock<IUnitOfWork>();
        uowMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var consumer = new OrderPaidEventConsumer(
            accountRepoMock.Object,
            userMembershipRepoMock.Object,
            packageRepoMock.Object,
            uowMock.Object,
            NullLogger<OrderPaidEventConsumer>.Instance,
            CreateIdempotencyMock().Object);

        var evt = CreateOrderPaidEvent(amount: 30m);

        // 不应抛异常
        await InvokeHandleAsync(consumer, evt);

        // ConfirmDeduct 仍应执行
        Assert.Equal(100, account.TotalSpent);
        // UserMembership 不应被激活
        Assert.Equal(UserMembershipStatus.Pending, userMembership.Status);
    }

    [Fact]
    public async Task HandleAsync_Should_Be_Idempotent_When_UserMembership_Already_Active_With_Same_OrderId()
    {
        var package = MembershipPackage.Create(
            PackageId, name: "月度会员", level: 1, price: 30m, durationDays: 30, benefits: "月度");
        var userMembership = UserMembership.Create(UserMembershipId, UserId, PackageId, level: 1);

        var accountRepoMock = new Mock<IPointsAccountRepository>();
        accountRepoMock.Setup(r => r.GetByFrozenOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PointsAccount?)null);

        var userMembershipRepoMock = new Mock<IUserMembershipRepository>();
        userMembershipRepoMock.Setup(r => r.GetByOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userMembership);

        var packageRepoMock = new Mock<IMembershipPackageRepository>();
        packageRepoMock.Setup(r => r.GetByIdAsync(PackageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(package);

        var uowMock = new Mock<IUnitOfWork>();
        uowMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var consumer = new OrderPaidEventConsumer(
            accountRepoMock.Object,
            userMembershipRepoMock.Object,
            packageRepoMock.Object,
            uowMock.Object,
            NullLogger<OrderPaidEventConsumer>.Instance,
            CreateIdempotencyMock().Object);

        // 首次激活（直接调用聚合根方法，模拟上一次事件已激活）
        userMembership.Activate(OrderId, DateTime.UtcNow, 30);
        Assert.Equal(UserMembershipStatus.Active, userMembership.Status);

        var evt = CreateOrderPaidEvent(amount: 30m);

        // 重复事件不应抛异常（幂等）
        await InvokeHandleAsync(consumer, evt);

        Assert.Equal(UserMembershipStatus.Active, userMembership.Status);
    }

    [Fact]
    public async Task HandleAsync_Should_Activate_When_All_Valid()
    {
        var package = MembershipPackage.Create(
            PackageId, name: "月度会员", level: 1, price: 30m, durationDays: 30, benefits: "月度");
        var userMembership = UserMembership.Create(UserMembershipId, UserId, PackageId, level: 1);

        var accountRepoMock = new Mock<IPointsAccountRepository>();
        accountRepoMock.Setup(r => r.GetByFrozenOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PointsAccount?)null);

        var userMembershipRepoMock = new Mock<IUserMembershipRepository>();
        userMembershipRepoMock.Setup(r => r.GetByOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userMembership);

        var packageRepoMock = new Mock<IMembershipPackageRepository>();
        packageRepoMock.Setup(r => r.GetByIdAsync(PackageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(package);

        var uowMock = new Mock<IUnitOfWork>();
        uowMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var consumer = new OrderPaidEventConsumer(
            accountRepoMock.Object,
            userMembershipRepoMock.Object,
            packageRepoMock.Object,
            uowMock.Object,
            NullLogger<OrderPaidEventConsumer>.Instance,
            CreateIdempotencyMock().Object);

        var evt = CreateOrderPaidEvent(amount: 30m);

        await InvokeHandleAsync(consumer, evt);

        Assert.Equal(UserMembershipStatus.Active, userMembership.Status);
        Assert.Equal(OrderId, userMembership.OrderId);
    }
}

/// <summary>
/// 直接验证 <see cref="UserMembership.Activate"/> 的 OrderId 幂等检查。
/// </summary>
public sealed class UserMembershipActivateIdempotencyTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid PackageId = Guid.NewGuid();
    private static readonly Guid UserMembershipId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();

    [Fact]
    public void Activate_Should_Be_Idempotent_When_Called_Twice_With_Same_OrderId()
    {
        var um = UserMembership.Create(UserMembershipId, UserId, PackageId, level: 1);
        var startTime = DateTime.UtcNow;

        um.Activate(OrderId, startTime, 30);
        Assert.Equal(UserMembershipStatus.Active, um.Status);

        // 重复调用不应抛异常且不应改变状态
        um.Activate(OrderId, startTime, 30);
        Assert.Equal(UserMembershipStatus.Active, um.Status);
        Assert.Equal(startTime, um.StartTime);
    }

    [Fact]
    public void Activate_Should_Throw_When_Already_Active_With_Different_OrderId()
    {
        var um = UserMembership.Create(UserMembershipId, UserId, PackageId, level: 1);
        um.Activate(OrderId, DateTime.UtcNow, 30);

        var act = () => um.Activate(Guid.NewGuid(), DateTime.UtcNow, 30);

        act.Should().Throw<PointsDomainException>().WithMessage("*不可激活*");
    }
}
