using Leno.Promotion.Domain.Aggregates;
using Leno.Promotion.Domain.Events;
using Leno.Promotion.Domain.Repositories;
using Leno.Promotion.Domain.Services;
using Leno.Promotion.Domain.ValueObjects;
using Leno.Promotion.Infrastructure.Consumers;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;

namespace Leno.Promotion.Infrastructure.Tests;

public class OrderPaidEventConsumerTests
{
    private readonly Mock<IUserCouponRepository> _userCouponRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<ILogger<OrderPaidEventConsumer>> _loggerMock = new();
    private readonly Mock<IConnectionMultiplexer> _redisMock = new();
    private readonly Mock<IDatabase> _redisDbMock = new();

    public OrderPaidEventConsumerTests()
    {
        _redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_redisDbMock.Object);
        _redisDbMock.Setup(d => d.KeyExistsAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).ReturnsAsync(false);
        _redisDbMock.Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>())).ReturnsAsync(true);
    }

    [Fact]
    public async Task Consume_ValidCoupon_ShouldConsume()
    {
        var orderId = Guid.NewGuid();
        var userCoupon = CreateUserCoupon();
        userCoupon.Lock(orderId);
        _userCouponRepoMock.Setup(r => r.GetByUsedOrderIdAsync(orderId, It.IsAny<CancellationToken>())).ReturnsAsync((UserCoupon?)null);
        _userCouponRepoMock.Setup(r => r.GetByLockedOrderIdAsync(orderId, It.IsAny<CancellationToken>())).ReturnsAsync(userCoupon);
        _unitOfWorkMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var consumer = new OrderPaidEventConsumer(
            _userCouponRepoMock.Object, _unitOfWorkMock.Object, _loggerMock.Object, _redisMock.Object);

        var evt = new OrderPaidEvent(orderId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Alipay", DateTime.UtcNow, "trade123", 100m, "CNY");
        var ctx = CreateConsumeContext(evt);
        await consumer.Consume(ctx);

        userCoupon.Status.Should().Be(CouponStatus.Used);
        _unitOfWorkMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Consume_AlreadyUsed_ShouldSkip()
    {
        var orderId = Guid.NewGuid();
        var userCoupon = CreateUserCoupon();
        userCoupon.Lock(orderId);
        userCoupon.Consume(orderId);
        _userCouponRepoMock.Setup(r => r.GetByUsedOrderIdAsync(orderId, It.IsAny<CancellationToken>())).ReturnsAsync(userCoupon);

        var consumer = new OrderPaidEventConsumer(
            _userCouponRepoMock.Object, _unitOfWorkMock.Object, _loggerMock.Object, _redisMock.Object);

        var evt = new OrderPaidEvent(orderId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Alipay", DateTime.UtcNow, "trade123", 100m, "CNY");
        var ctx = CreateConsumeContext(evt);
        await consumer.Consume(ctx);

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

public class OrderCancelledEventConsumerTests
{
    private readonly Mock<IUserCouponRepository> _userCouponRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<ILogger<OrderCancelledEventConsumer>> _loggerMock = new();
    private readonly Mock<IConnectionMultiplexer> _redisMock = new();
    private readonly Mock<IDatabase> _redisDbMock = new();

    public OrderCancelledEventConsumerTests()
    {
        _redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_redisDbMock.Object);
        _redisDbMock.Setup(d => d.KeyExistsAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).ReturnsAsync(false);
        _redisDbMock.Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>())).ReturnsAsync(true);
    }

    [Fact]
    public async Task Consume_ValidCoupon_ShouldRelease()
    {
        var orderId = Guid.NewGuid();
        var userCoupon = CreateUserCoupon();
        userCoupon.Lock(orderId);
        _userCouponRepoMock.Setup(r => r.GetByLockedOrderIdAsync(orderId, It.IsAny<CancellationToken>())).ReturnsAsync(userCoupon);
        _unitOfWorkMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var consumer = new OrderCancelledEventConsumer(
            _userCouponRepoMock.Object, _unitOfWorkMock.Object, _loggerMock.Object, _redisMock.Object);

        var evt = new OrderCancelledEvent(orderId, Guid.NewGuid(), "cancel", DateTime.UtcNow, "Buyer", 0);
        await consumer.Consume(CreateConsumeContext(evt));

        userCoupon.Status.Should().Be(CouponStatus.Unused);
        _unitOfWorkMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Consume_NoCoupon_ShouldSkip()
    {
        var orderId = Guid.NewGuid();
        _userCouponRepoMock.Setup(r => r.GetByLockedOrderIdAsync(orderId, It.IsAny<CancellationToken>())).ReturnsAsync((UserCoupon?)null);

        var consumer = new OrderCancelledEventConsumer(
            _userCouponRepoMock.Object, _unitOfWorkMock.Object, _loggerMock.Object, _redisMock.Object);

        var evt = new OrderCancelledEvent(orderId, Guid.NewGuid(), "cancel", DateTime.UtcNow, "Buyer", 0);
        await consumer.Consume(CreateConsumeContext(evt));

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

public class RefundCompletedEventConsumerTests
{
    private readonly Mock<IUserCouponRepository> _userCouponRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<ILogger<RefundCompletedEventConsumer>> _loggerMock = new();
    private readonly Mock<IConnectionMultiplexer> _redisMock = new();
    private readonly Mock<IDatabase> _redisDbMock = new();

    public RefundCompletedEventConsumerTests()
    {
        _redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_redisDbMock.Object);
        _redisDbMock.Setup(d => d.KeyExistsAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).ReturnsAsync(false);
        _redisDbMock.Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>())).ReturnsAsync(true);
    }

    [Fact]
    public async Task Consume_ValidCoupon_ShouldReturn()
    {
        var orderId = Guid.NewGuid();
        var userCoupon = CreateUsedCoupon(orderId);
        _userCouponRepoMock.Setup(r => r.GetByUsedOrderIdAsync(orderId, It.IsAny<CancellationToken>())).ReturnsAsync(userCoupon);
        _unitOfWorkMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var consumer = new RefundCompletedEventConsumer(
            _userCouponRepoMock.Object, _unitOfWorkMock.Object, _loggerMock.Object, _redisMock.Object);

        var evt = new RefundCompletedEvent(orderId, Guid.NewGuid(), Guid.NewGuid(), 100m, "CNY", DateTime.UtcNow);
        await consumer.Consume(CreateConsumeContext(evt));

        userCoupon.Status.Should().Be(CouponStatus.Unused);
        _unitOfWorkMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Consume_NoCoupon_ShouldSkip()
    {
        var orderId = Guid.NewGuid();
        _userCouponRepoMock.Setup(r => r.GetByUsedOrderIdAsync(orderId, It.IsAny<CancellationToken>())).ReturnsAsync((UserCoupon?)null);

        var consumer = new RefundCompletedEventConsumer(
            _userCouponRepoMock.Object, _unitOfWorkMock.Object, _loggerMock.Object, _redisMock.Object);

        var evt = new RefundCompletedEvent(orderId, Guid.NewGuid(), Guid.NewGuid(), 100m, "CNY", DateTime.UtcNow);
        await consumer.Consume(CreateConsumeContext(evt));

        _unitOfWorkMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static UserCoupon CreateUsedCoupon(Guid orderId)
    {
        var uc = UserCoupon.Receive(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Manual", DateTime.UtcNow.AddDays(30));
        uc.Lock(orderId);
        uc.Consume(orderId);
        return uc;
    }

    private static ConsumeContext<T> CreateConsumeContext<T>(T message) where T : class
    {
        var mock = new Mock<ConsumeContext<T>>();
        mock.Setup(c => c.Message).Returns(message);
        mock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        return mock.Object;
    }
}

public class PointsExchangeConsumerTests
{
    private readonly Mock<ICouponRepository> _couponRepoMock = new();
    private readonly Mock<IUserCouponRepository> _userCouponRepoMock = new();
    private readonly Mock<ILogger<PointsExchangeConsumer>> _loggerMock = new();
    private readonly Mock<IConnectionMultiplexer> _redisMock = new();
    private readonly Mock<IDatabase> _redisDbMock = new();

    public PointsExchangeConsumerTests()
    {
        _redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_redisDbMock.Object);
        _redisDbMock.Setup(d => d.KeyExistsAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).ReturnsAsync(false);
        _redisDbMock.Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>())).ReturnsAsync(true);
    }

    [Fact]
    public async Task Consume_CouponNotFound_ShouldSkip()
    {
        var couponId = Guid.NewGuid();
        _couponRepoMock.Setup(r => r.GetByIdAsync(couponId, It.IsAny<CancellationToken>())).ReturnsAsync((Coupon?)null);

        var mockDb = new Mock<PromotionDbContext>(new DbContextOptions<PromotionDbContext>());
        var consumer = new PointsExchangeConsumer(
            _couponRepoMock.Object, _userCouponRepoMock.Object, mockDb.Object, _loggerMock.Object, _redisMock.Object);

        var evt = new PointsExchangeCouponRequestedEvent(Guid.NewGuid(), Guid.NewGuid(), couponId, 100);
        await consumer.Consume(CreateConsumeContext(evt));

        _userCouponRepoMock.Verify(r => r.AddAsync(It.IsAny<UserCoupon>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Consume_DisabledCoupon_ShouldSkip()
    {
        var couponId = Guid.NewGuid();
        var coupon = Coupon.Create(couponId, "points", CouponType.FixedAmount, 10m, 0m,
            CouponValidityType.RelativeDays, null, null, 7, 1000);
        coupon.Disable();
        _couponRepoMock.Setup(r => r.GetByIdAsync(couponId, It.IsAny<CancellationToken>())).ReturnsAsync(coupon);

        var mockDb = new Mock<PromotionDbContext>(new DbContextOptions<PromotionDbContext>());
        var consumer = new PointsExchangeConsumer(
            _couponRepoMock.Object, _userCouponRepoMock.Object, mockDb.Object, _loggerMock.Object, _redisMock.Object);

        var evt = new PointsExchangeCouponRequestedEvent(Guid.NewGuid(), Guid.NewGuid(), couponId, 100);
        await consumer.Consume(CreateConsumeContext(evt));

        _userCouponRepoMock.Verify(r => r.AddAsync(It.IsAny<UserCoupon>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Consume_ExceededCoupon_ShouldSkip()
    {
        var couponId = Guid.NewGuid();
        var coupon = Coupon.Create(couponId, "points", CouponType.FixedAmount, 10m, 0m,
            CouponValidityType.RelativeDays, null, null, 7, 1);
        coupon.Issue(1);
        _couponRepoMock.Setup(r => r.GetByIdAsync(couponId, It.IsAny<CancellationToken>())).ReturnsAsync(coupon);

        var mockDb = new Mock<PromotionDbContext>(new DbContextOptions<PromotionDbContext>());
        var consumer = new PointsExchangeConsumer(
            _couponRepoMock.Object, _userCouponRepoMock.Object, mockDb.Object, _loggerMock.Object, _redisMock.Object);

        var evt = new PointsExchangeCouponRequestedEvent(Guid.NewGuid(), Guid.NewGuid(), couponId, 100);
        await consumer.Consume(CreateConsumeContext(evt));

        _userCouponRepoMock.Verify(r => r.AddAsync(It.IsAny<UserCoupon>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static ConsumeContext<T> CreateConsumeContext<T>(T message) where T : class
    {
        var mock = new Mock<ConsumeContext<T>>();
        mock.Setup(c => c.Message).Returns(message);
        mock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        return mock.Object;
    }
}

public class SeckillOrderCreationFailedEventConsumerTests
{
    private readonly Mock<ISeckillActivityRepository> _activityRepoMock = new();
    private readonly Mock<ISeckillStockService> _stockServiceMock = new();
    private readonly Mock<ISeckillPreOccupationRecordRepository> _preOccupationRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<ILogger<SeckillOrderCreationFailedEventConsumer>> _loggerMock = new();
    private readonly Mock<IConnectionMultiplexer> _redisMock = new();
    private readonly Mock<IDatabase> _redisDbMock = new();

    public SeckillOrderCreationFailedEventConsumerTests()
    {
        _redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_redisDbMock.Object);
        _redisDbMock.Setup(d => d.KeyExistsAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).ReturnsAsync(false);
        _redisDbMock.Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>())).ReturnsAsync(true);
    }

    [Fact]
    public async Task Consume_ShouldRollbackRedisAndDb()
    {
        var activityId = Guid.NewGuid();
        var skuId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var activity = SeckillActivity.Create(activityId, Guid.NewGuid(), skuId, 99m, 199m, 100, 1,
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(2));
        activity.Activate();
        activity.DeductStock(Guid.NewGuid(), 5);

        _activityRepoMock.Setup(r => r.GetByIdAsync(activityId, It.IsAny<CancellationToken>())).ReturnsAsync(activity);
        _stockServiceMock.Setup(s => s.RestoreAsync(activityId, skuId, 5, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var consumer = new SeckillOrderCreationFailedEventConsumer(
            _activityRepoMock.Object, _stockServiceMock.Object, _preOccupationRepoMock.Object,
            _unitOfWorkMock.Object, _loggerMock.Object, _redisMock.Object);

        var evt = new SeckillOrderCreationFailedEvent(activityId, skuId, Guid.NewGuid(), orderId, 5, "fail");
        await consumer.Consume(CreateConsumeContext(evt));

        _stockServiceMock.Verify(s => s.RestoreAsync(activityId, skuId, 5, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static ConsumeContext<T> CreateConsumeContext<T>(T message) where T : class
    {
        var mock = new Mock<ConsumeContext<T>>();
        mock.Setup(c => c.Message).Returns(message);
        mock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        return mock.Object;
    }
}