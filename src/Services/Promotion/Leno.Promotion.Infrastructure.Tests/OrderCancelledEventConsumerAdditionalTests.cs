using Leno.Promotion.Domain.Aggregates;
using Leno.Promotion.Domain.Repositories;
using Leno.Promotion.Domain.ValueObjects;
using Leno.Promotion.Infrastructure.Consumers;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using MassTransit;
using Microsoft.Extensions.Logging;
using Leno.Infrastructure.Abstractions;
using Moq;

namespace Leno.Promotion.Infrastructure.Tests;

/// <summary>
/// P0-2.6 补充测试：覆盖 OrderCancelledEventConsumer 在券已核销/非 Locked 态时的幂等跳过行为。
/// </summary>
public class OrderCancelledEventConsumerAdditionalTests
{
    private readonly Mock<IUserCouponRepository> _userCouponRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<ILogger<OrderCancelledEventConsumer>> _loggerMock = new();
    private readonly Mock<IIdempotencyStore> _idempotencyStoreMock = new();

    public OrderCancelledEventConsumerAdditionalTests()
    {
        _idempotencyStoreMock.Setup(s => s.IsProcessedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _idempotencyStoreMock.Setup(s => s.MarkAsProcessedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task Consume_CouponAlreadyUsed_ShouldSkipWithoutThrowing()
    {
        // 业务场景：订单先支付（券 Locked→Used）后又被取消（如退款流程触发取消事件）
        // 此时 Release 会抛 USER_COUPON_RELEASE_INVALID，应改为跳过并记录日志，不应死信
        var orderId = Guid.NewGuid();
        var userCoupon = CreateUserCoupon();
        userCoupon.Lock(orderId);
        userCoupon.Consume(orderId); // 模拟券已核销
        _userCouponRepoMock.Setup(r => r.GetByLockedOrderIdAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userCoupon);
        _unitOfWorkMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var consumer = new OrderCancelledEventConsumer(
            _userCouponRepoMock.Object, _unitOfWorkMock.Object, _loggerMock.Object, _idempotencyStoreMock.Object);

        var evt = new OrderCancelledEvent(orderId, Guid.NewGuid(), "refund-triggered-cancel", DateTime.UtcNow, "System", 0);

        // 关键断言：不应抛异常（避免 MassTransit 死信）
        var act = () => consumer.Consume(CreateConsumeContext(evt));
        await act.Should().NotThrowAsync();

        userCoupon.Status.Should().Be(CouponStatus.Used); // 状态保持 Used，由 RefundCompleted 退还
        _unitOfWorkMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Consume_CouponStatusNotLocked_ShouldIdempotentSkip()
    {
        // 防御性：券已 Expired 或其他非 Locked 状态时幂等跳过
        var orderId = Guid.NewGuid();
        var userCoupon = CreateUserCoupon();
        userCoupon.Expire(); // 已 Expired
        // 注意 Expire 会清空 LockedOrderId，因此仓储查询不会命中
        // 此测试覆盖其他潜在非 Locked 场景（如直接 mock 返回 Expired 状态）
        _userCouponRepoMock.Setup(r => r.GetByLockedOrderIdAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userCoupon);

        var consumer = new OrderCancelledEventConsumer(
            _userCouponRepoMock.Object, _unitOfWorkMock.Object, _loggerMock.Object, _idempotencyStoreMock.Object);

        var evt = new OrderCancelledEvent(orderId, Guid.NewGuid(), "cancel", DateTime.UtcNow, "Buyer", 0);

        var act = () => consumer.Consume(CreateConsumeContext(evt));
        await act.Should().NotThrowAsync();
        _unitOfWorkMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static UserCoupon CreateUserCoupon()
        => UserCoupon.Receive(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Manual", DateTime.UtcNow.AddDays(30));

    private static ConsumeContext<T> CreateConsumeContext<T>(T message) where T : class
    {
        var mock = new Mock<ConsumeContext<T>>();
        mock.Setup(c => c.Message).Returns(message);
        mock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        return mock.Object;
    }
}
