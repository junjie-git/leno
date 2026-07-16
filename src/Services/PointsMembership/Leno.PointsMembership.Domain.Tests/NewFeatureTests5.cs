using Leno.PointsMembership.Domain.Aggregates;
using Leno.PointsMembership.Domain.Repositories;
using Leno.PointsMembership.Infrastructure.Consumers;
using Leno.PointsMembership.Domain.ValueObjects;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Leno.Infrastructure.Abstractions;
using Microsoft.Extensions.Logging;
using Moq;
using System.Reflection;

namespace Leno.PointsMembership.Domain.Tests;

#region RefundCompletedEventConsumer Tests

public class RefundCompletedEventConsumerTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid RefundId = Guid.NewGuid();

    private static async Task InvokeHandleAsync(
        RefundCompletedEventConsumer consumer,
        RefundCompletedEvent evt,
        CancellationToken ct = default)
    {
        var method = typeof(RefundCompletedEventConsumer)
            .GetMethod("HandleAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await (Task)method.Invoke(consumer, [evt, ct])!;
    }

    [Fact]
    public async Task HandleAsync_ValidEvent_ShouldRevertPoints()
    {
        var evt = new RefundCompletedEvent(OrderId, UserId, RefundId, 150m, "CNY", DateTime.UtcNow);
        var account = PointsAccount.Create(Guid.NewGuid(), UserId);
        account.Earn(PointsSource.Consumption, 200, "");

        var accountRepoMock = new Mock<IPointsAccountRepository>();
        accountRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var uowMock = new Mock<IUnitOfWork>();
        var loggerMock = new Mock<ILogger<RefundCompletedEventConsumer>>();
        var idempotencyStoreMock = new Mock<IIdempotencyStore>();

        var consumer = new RefundCompletedEventConsumer(
            accountRepoMock.Object, uowMock.Object, loggerMock.Object, idempotencyStoreMock.Object);

        await InvokeHandleAsync(consumer, evt);

        account.Balance.Should().Be(50);
        uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_RefundExceedsBalance_ShouldAllowNegative()
    {
        var evt = new RefundCompletedEvent(OrderId, UserId, RefundId, 300m, "CNY", DateTime.UtcNow);
        var account = PointsAccount.Create(Guid.NewGuid(), UserId);
        account.Earn(PointsSource.Consumption, 100, "");

        var accountRepoMock = new Mock<IPointsAccountRepository>();
        accountRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var uowMock = new Mock<IUnitOfWork>();
        var loggerMock = new Mock<ILogger<RefundCompletedEventConsumer>>();
        var idempotencyStoreMock = new Mock<IIdempotencyStore>();

        var consumer = new RefundCompletedEventConsumer(
            accountRepoMock.Object, uowMock.Object, loggerMock.Object, idempotencyStoreMock.Object);

        await InvokeHandleAsync(consumer, evt);

        account.Balance.Should().Be(-200);
    }

    [Fact]
    public async Task HandleAsync_ZeroRefundAmount_ShouldNotRevert()
    {
        var evt = new RefundCompletedEvent(OrderId, UserId, RefundId, 0m, "CNY", DateTime.UtcNow);
        var account = PointsAccount.Create(Guid.NewGuid(), UserId);

        var accountRepoMock = new Mock<IPointsAccountRepository>();
        accountRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var uowMock = new Mock<IUnitOfWork>();
        var loggerMock = new Mock<ILogger<RefundCompletedEventConsumer>>();
        var idempotencyStoreMock = new Mock<IIdempotencyStore>();

        var consumer = new RefundCompletedEventConsumer(
            accountRepoMock.Object, uowMock.Object, loggerMock.Object, idempotencyStoreMock.Object);

        await InvokeHandleAsync(consumer, evt);

        account.Balance.Should().Be(0);
        uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_AccountNotFound_ShouldNotThrow()
    {
        var evt = new RefundCompletedEvent(OrderId, UserId, RefundId, 100m, "CNY", DateTime.UtcNow);

        var accountRepoMock = new Mock<IPointsAccountRepository>();
        accountRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PointsAccount?)null);

        var uowMock = new Mock<IUnitOfWork>();
        var loggerMock = new Mock<ILogger<RefundCompletedEventConsumer>>();
        var idempotencyStoreMock = new Mock<IIdempotencyStore>();

        var consumer = new RefundCompletedEventConsumer(
            accountRepoMock.Object, uowMock.Object, loggerMock.Object, idempotencyStoreMock.Object);

        await InvokeHandleAsync(consumer, evt);

        uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}

#endregion

