using Leno.PointsMembership.Domain.Aggregates;
using Leno.PointsMembership.Domain.Repositories;
using Leno.PointsMembership.Infrastructure.Consumers;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Leno.Infrastructure.Abstractions;
using Microsoft.Extensions.Logging;
using Moq;
using System.Reflection;

namespace Leno.PointsMembership.Domain.Tests;

#region UserRegisteredEventConsumer Tests

public class UserRegisteredEventConsumerNewUserPointsTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private static async Task InvokeHandleAsync(
        UserRegisteredEventConsumer consumer,
        UserRegisteredEvent evt,
        CancellationToken ct = default)
    {
        var method = typeof(UserRegisteredEventConsumer)
            .GetMethod("HandleAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await (Task)method.Invoke(consumer, [evt, ct])!;
    }

    [Fact]
    public async Task HandleAsync_NewUser_ShouldGrantNewUserPoints()
    {
        var evt = new UserRegisteredEvent(UserId, "test", null, null);

        var accountRepoMock = new Mock<IPointsAccountRepository>();
        accountRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PointsAccount?)null);

        var memberRepoMock = new Mock<IMemberRepository>();
        var uowMock = new Mock<IUnitOfWork>();
        var loggerMock = new Mock<ILogger<UserRegisteredEventConsumer>>();
        var idempotencyStoreMock = new Mock<IIdempotencyStore>();

        var consumer = new UserRegisteredEventConsumer(
            accountRepoMock.Object, memberRepoMock.Object, uowMock.Object, loggerMock.Object, idempotencyStoreMock.Object);

        await InvokeHandleAsync(consumer, evt);

        uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
        accountRepoMock.Verify(r => r.AddAsync(It.IsAny<PointsAccount>(), It.IsAny<CancellationToken>()), Times.Once);
        memberRepoMock.Verify(r => r.AddAsync(It.IsAny<Member>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ExistingAccount_ShouldSkip()
    {
        var evt = new UserRegisteredEvent(UserId, "test", null, null);
        var existingAccount = PointsAccount.Create(Guid.NewGuid(), UserId);

        var accountRepoMock = new Mock<IPointsAccountRepository>();
        accountRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingAccount);

        var memberRepoMock = new Mock<IMemberRepository>();
        var uowMock = new Mock<IUnitOfWork>();
        var loggerMock = new Mock<ILogger<UserRegisteredEventConsumer>>();
        var idempotencyStoreMock = new Mock<IIdempotencyStore>();

        var consumer = new UserRegisteredEventConsumer(
            accountRepoMock.Object, memberRepoMock.Object, uowMock.Object, loggerMock.Object, idempotencyStoreMock.Object);

        await InvokeHandleAsync(consumer, evt);

        uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
        accountRepoMock.Verify(r => r.AddAsync(It.IsAny<PointsAccount>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

#endregion
