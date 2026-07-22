using Leno.Infrastructure.Abstractions;
using Leno.Infrastructure.EventBus;
using Leno.PointsMembership.Domain.Aggregates;
using Leno.PointsMembership.Domain.Repositories;
using Leno.PointsMembership.Infrastructure.Consumers;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Reflection;
using Xunit;

namespace Leno.PointsMembership.Domain.Tests;

/// <summary>
/// 验证 PM-M04 修复：<see cref="OrderPaidEventConsumer"/> 在 SaveEntitiesAsync 抛出
/// <see cref="DbUpdateConcurrencyException"/> 时不重抛，视为并发实例已处理。
/// </summary>
public sealed class OrderPaidEventConsumerConcurrencyTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();

    private static async Task InvokeHandleAsync(
        OrderPaidEventConsumer consumer,
        OrderPaidEvent evt,
        CancellationToken ct = default)
    {
        var method = typeof(OrderPaidEventConsumer)
            .GetMethod("HandleAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await (Task)method.Invoke(consumer, [evt, ct])!;
    }

    [Fact]
    public async Task HandleAsync_Should_Swallow_DbUpdateConcurrencyException()
    {
        var accountRepoMock = new Mock<IPointsAccountRepository>();
        accountRepoMock.Setup(r => r.GetByFrozenOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PointsAccount?)null);

        var userMembershipRepoMock = new Mock<IUserMembershipRepository>();
        userMembershipRepoMock.Setup(r => r.GetByOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserMembership?)null);

        var packageRepoMock = new Mock<IMembershipPackageRepository>();

        var uowMock = new Mock<IUnitOfWork>();
        // 模拟并发冲突：SaveEntitiesAsync 抛 DbUpdateConcurrencyException
        uowMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException("并发冲突"));

        var idempotencyMock = new Mock<IIdempotencyStore>();
        idempotencyMock.Setup(s => s.IsProcessedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        idempotencyMock.Setup(s => s.MarkAsProcessedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var consumer = new OrderPaidEventConsumer(
            accountRepoMock.Object,
            userMembershipRepoMock.Object,
            packageRepoMock.Object,
            uowMock.Object,
            NullLogger<OrderPaidEventConsumer>.Instance,
            idempotencyMock.Object);

        var evt = new OrderPaidEvent(
            orderId: OrderId,
            userId: UserId,
            sellerId: Guid.NewGuid(),
            paymentId: Guid.NewGuid(),
            channel: "alipay",
            paidAt: DateTime.UtcNow,
            tradeNo: "TRADE001",
            amount: 100m,
            currency: "CNY");

        // 不应抛出 DbUpdateConcurrencyException，应被捕获
        await InvokeHandleAsync(consumer, evt);

        // 验证 SaveEntitiesAsync 确实被调用
        uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
