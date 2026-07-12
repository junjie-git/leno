using Leno.PointsMembership.Domain.Aggregates;
using Leno.PointsMembership.Domain.Events;
using Leno.PointsMembership.Domain.ValueObjects;
using Leno.PointsMembership.Domain.Repositories;
using Leno.PointsMembership.Infrastructure.Consumers;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using System.Reflection;

namespace Leno.PointsMembership.Domain.Tests;

#region CouponExchangeConsumer Tests

public class CouponExchangeConsumerTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid ExchangeId = Guid.NewGuid();
    private static readonly Guid CouponId = Guid.NewGuid();

    private static async Task InvokeHandleAsync<T>(object consumer, T evt, CancellationToken ct = default)
    {
        var method = consumer.GetType().GetMethod("HandleAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await (Task)method.Invoke(consumer, [evt, ct])!;
    }

    [Fact]
    public async Task CouponExchangeSucceeded_ShouldConfirmDeduct()
    {
        var evt = new CouponExchangeSucceededEvent(ExchangeId, UserId, CouponId);
        var account = PointsAccount.Create(Guid.NewGuid(), UserId);
        account.Earn(PointsSource.CheckIn, 500, "");
        account.Freeze(100, ExchangeId);

        var accountRepoMock = new Mock<IPointsAccountRepository>();
        accountRepoMock.Setup(r => r.GetByFrozenOrderIdAsync(ExchangeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var uowMock = new Mock<IUnitOfWork>();
        var loggerMock = new Mock<ILogger<CouponExchangeSucceededEventConsumer>>();
        var redisMock = new Mock<IConnectionMultiplexer>();

        var consumer = new CouponExchangeSucceededEventConsumer(
            accountRepoMock.Object, uowMock.Object, loggerMock.Object, redisMock.Object);

        await InvokeHandleAsync(consumer, evt);

        account.FrozenBalance.Should().Be(0);
        account.TotalSpent.Should().Be(100);
        uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CouponExchangeSucceeded_NoFrozenEntry_ShouldSkip()
    {
        var evt = new CouponExchangeSucceededEvent(ExchangeId, UserId, CouponId);

        var accountRepoMock = new Mock<IPointsAccountRepository>();
        accountRepoMock.Setup(r => r.GetByFrozenOrderIdAsync(ExchangeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PointsAccount?)null);

        var uowMock = new Mock<IUnitOfWork>();
        var loggerMock = new Mock<ILogger<CouponExchangeSucceededEventConsumer>>();
        var redisMock = new Mock<IConnectionMultiplexer>();

        var consumer = new CouponExchangeSucceededEventConsumer(
            accountRepoMock.Object, uowMock.Object, loggerMock.Object, redisMock.Object);

        await InvokeHandleAsync(consumer, evt);

        uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CouponExchangeFailed_ShouldReleaseFrozenPoints()
    {
        var evt = new CouponExchangeFailedEvent(ExchangeId, UserId, "库存不足");
        var account = PointsAccount.Create(Guid.NewGuid(), UserId);
        account.Earn(PointsSource.CheckIn, 500, "");
        account.Freeze(100, ExchangeId);

        var accountRepoMock = new Mock<IPointsAccountRepository>();
        accountRepoMock.Setup(r => r.GetByFrozenOrderIdAsync(ExchangeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var uowMock = new Mock<IUnitOfWork>();
        var loggerMock = new Mock<ILogger<CouponExchangeFailedEventConsumer>>();
        var redisMock = new Mock<IConnectionMultiplexer>();

        var consumer = new CouponExchangeFailedEventConsumer(
            accountRepoMock.Object, uowMock.Object, loggerMock.Object, redisMock.Object);

        await InvokeHandleAsync(consumer, evt);

        account.FrozenBalance.Should().Be(0);
        account.Balance.Should().Be(500);
        uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CouponExchangeFailed_NoFrozenEntry_ShouldSkip()
    {
        var evt = new CouponExchangeFailedEvent(ExchangeId, UserId, "超时");

        var accountRepoMock = new Mock<IPointsAccountRepository>();
        accountRepoMock.Setup(r => r.GetByFrozenOrderIdAsync(ExchangeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PointsAccount?)null);

        var uowMock = new Mock<IUnitOfWork>();
        var loggerMock = new Mock<ILogger<CouponExchangeFailedEventConsumer>>();
        var redisMock = new Mock<IConnectionMultiplexer>();

        var consumer = new CouponExchangeFailedEventConsumer(
            accountRepoMock.Object, uowMock.Object, loggerMock.Object, redisMock.Object);

        await InvokeHandleAsync(consumer, evt);

        uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}

#endregion

#region CouponEvents Tests

public class CouponEventsTests
{
    [Fact]
    public void PointsExchangeCouponRequestedEvent_ShouldInitializeCorrectly()
    {
        var exchangeId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var templateId = Guid.NewGuid();

        var evt = new PointsExchangeCouponRequestedEvent(exchangeId, userId, templateId, 200);

        evt.ExchangeId.Should().Be(exchangeId);
        evt.UserId.Should().Be(userId);
        evt.CouponTemplateId.Should().Be(templateId);
        evt.PointsRequired.Should().Be(200);
        evt.EventId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void CouponExchangeSucceededEvent_ShouldInitializeCorrectly()
    {
        var exchangeId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var couponId = Guid.NewGuid();

        var evt = new CouponExchangeSucceededEvent(exchangeId, userId, couponId);

        evt.ExchangeId.Should().Be(exchangeId);
        evt.UserId.Should().Be(userId);
        evt.CouponId.Should().Be(couponId);
    }

    [Fact]
    public void CouponExchangeFailedEvent_ShouldInitializeCorrectly()
    {
        var exchangeId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var evt = new CouponExchangeFailedEvent(exchangeId, userId, "库存不足");

        evt.ExchangeId.Should().Be(exchangeId);
        evt.UserId.Should().Be(userId);
        evt.Reason.Should().Be("库存不足");
    }

    [Fact]
    public void PointsConsumedEvent_ShouldInitializeCorrectly()
    {
        var accountId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var referenceId = Guid.NewGuid();

        var evt = new PointsConsumedEvent(accountId, userId, 50, referenceId, "兑换");

        evt.AccountId.Should().Be(accountId);
        evt.UserId.Should().Be(userId);
        evt.Amount.Should().Be(50);
        evt.ReferenceId.Should().Be(referenceId);
        evt.Reason.Should().Be("兑换");
    }

    [Fact]
    public void PointsRevertedEvent_ShouldInitializeCorrectly()
    {
        var accountId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var referenceId = Guid.NewGuid();

        var evt = new PointsRevertedEvent(accountId, userId, 100, referenceId, "退款扣回");

        evt.AccountId.Should().Be(accountId);
        evt.UserId.Should().Be(userId);
        evt.Amount.Should().Be(100);
        evt.ReferenceId.Should().Be(referenceId);
        evt.Reason.Should().Be("退款扣回");
    }
}

#endregion

