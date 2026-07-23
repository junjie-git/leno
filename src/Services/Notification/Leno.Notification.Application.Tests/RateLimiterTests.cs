using Leno.Notification.Domain.Services;
using Leno.Notification.Domain.ValueObjects;
using Leno.Notification.Infrastructure.Options;
using Leno.Notification.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;

namespace Leno.Notification.Application.Tests;

public class RateLimiterTests
{
    private readonly Mock<IConnectionMultiplexer> _redisMock = new();
    private readonly Mock<ILogger<RedisRateLimiter>> _loggerMock = new();
    private readonly Mock<IDatabase> _databaseMock = new();
    private readonly Mock<IOptionsMonitor<RateLimitOptions>> _optionsMock = new();

    private RedisRateLimiter CreateSut()
    {
        _redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_databaseMock.Object);
        _optionsMock.Setup(o => o.CurrentValue).Returns(new RateLimitOptions());
        return new RedisRateLimiter(_redisMock.Object, _optionsMock.Object, _loggerMock.Object);
    }

    #region AcquireAsync - Email Channel

    [Fact]
    public async Task AcquireAsync_EmailWithinLimit_ShouldAllow()
    {
        // Arrange
        var sut = CreateSut();
        _databaseMock.Setup(d => d.CreateTransaction(It.IsAny<object>())).Returns(Mock.Of<ITransaction>());

        // Act
        var result = await sut.AcquireAsync("user@example.com", "OrderCreated", NotificationChannel.Email);

        // Assert
        result.Allowed.Should().BeTrue();
    }

    #endregion

    #region AcquireAsync - Over Limit (Transaction Tasks Explicit Await)

    [Fact]
    public async Task AcquireAsync_OverLimit_ShouldDeny()
    {
        // Arrange
        var transactionMock = CreateTransactionMock();
        transactionMock.Setup(t => t.ExecuteAsync(It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
        transactionMock.Setup(t => t.SortedSetLengthAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<double>(),
                It.IsAny<double>(),
                It.IsAny<Exclude>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(11L);

        _databaseMock.Setup(d => d.CreateTransaction(It.IsAny<object>())).Returns(transactionMock.Object);
        var sut = CreateSut();

        // Act
        var result = await sut.AcquireAsync("user@example.com", "OrderCreated", NotificationChannel.Email);

        // Assert
        result.Allowed.Should().BeFalse();
        result.ErrorCode.Should().Be("RATE_LIMITED");
        result.CurrentCount.Should().Be(11);
        result.Limit.Should().Be(10);
    }

    [Fact]
    public async Task AcquireAsync_TransactionExecuteFails_ShouldDegradeToAllow()
    {
        // Arrange
        var transactionMock = CreateTransactionMock();
        transactionMock.Setup(t => t.ExecuteAsync(It.IsAny<CommandFlags>()))
            .ReturnsAsync(false); // 事务未执行
        // 即使 countTask 返回超限值，也应因事务未执行而 fail-open
        transactionMock.Setup(t => t.SortedSetLengthAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<double>(),
                It.IsAny<double>(),
                It.IsAny<Exclude>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(11L);

        _databaseMock.Setup(d => d.CreateTransaction(It.IsAny<object>())).Returns(transactionMock.Object);
        var sut = CreateSut();

        // Act
        var result = await sut.AcquireAsync("user@example.com", "OrderCreated", NotificationChannel.Email);

        // Assert
        result.Allowed.Should().BeTrue(); // fail-open 降级
    }

    [Fact]
    public async Task AcquireAsync_SmsChannel_ShouldApplyHourAndDayLimits()
    {
        // Arrange - SMS 双重限流：先检查小时（5条/小时），再检查日（20条/天）
        var hourlyTransactionMock = CreateTransactionMock();
        hourlyTransactionMock.Setup(t => t.ExecuteAsync(It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
        hourlyTransactionMock.Setup(t => t.SortedSetLengthAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<double>(),
                It.IsAny<double>(),
                It.IsAny<Exclude>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(4L); // 小时未超 5

        var dailyTransactionMock = CreateTransactionMock();
        dailyTransactionMock.Setup(t => t.ExecuteAsync(It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
        dailyTransactionMock.Setup(t => t.SortedSetLengthAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<double>(),
                It.IsAny<double>(),
                It.IsAny<Exclude>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(21L); // 日超 20

        _databaseMock.SetupSequence(d => d.CreateTransaction(It.IsAny<object>()))
            .Returns(hourlyTransactionMock.Object)
            .Returns(dailyTransactionMock.Object);

        var sut = CreateSut();

        // Act
        var result = await sut.AcquireAsync("13900001111", "OrderCreated", NotificationChannel.Sms);

        // Assert - 被日限流拦截
        result.Allowed.Should().BeFalse();
        result.ErrorCode.Should().Be("RATE_LIMITED");
        result.CurrentCount.Should().Be(21);
        result.Limit.Should().Be(20);
    }

    #endregion

    /// <summary>
    /// 创建一个松散的 ITransaction mock，未显式 Setup 的方法返回默认值。
    /// </summary>
    private static Mock<ITransaction> CreateTransactionMock()
    {
        var mock = new Mock<ITransaction>(MockBehavior.Loose);

        // 显式 Setup 排除范围的移除/添加/过期任务，避免被遗漏导致 NRE
        mock.Setup(t => t.SortedSetRemoveRangeByScoreAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<double>(),
                It.IsAny<double>(),
                It.IsAny<Exclude>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(0L);
        mock.Setup(t => t.SortedSetAddAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<double>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
        mock.Setup(t => t.KeyExpireAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        return mock;
    }

    #region AcquireAsync - InApp Channel

    [Fact]
    public async Task AcquireAsync_InAppChannel_ShouldAlwaysAllow()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.AcquireAsync("user-123", "OrderCreated", NotificationChannel.InApp);

        // Assert
        result.Allowed.Should().BeTrue();
    }

    #endregion

    #region AcquireAsync - Empty Recipient

    [Fact]
    public async Task AcquireAsync_EmptyRecipient_ShouldAllow()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.AcquireAsync("", "OrderCreated", NotificationChannel.Email);

        // Assert
        result.Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task AcquireAsync_NullRecipient_ShouldAllow()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.AcquireAsync(null!, "OrderCreated", NotificationChannel.Email);

        // Assert
        result.Allowed.Should().BeTrue();
    }

    #endregion

    #region AcquireAsync - Redis Unavailable (Degradation)

    [Fact]
    public async Task AcquireAsync_RedisThrowsException_ShouldDegradeToAllow()
    {
        // Arrange
        _redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Throws(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Connection failed"));
        _optionsMock.Setup(o => o.CurrentValue).Returns(new RateLimitOptions());
        var sut = new RedisRateLimiter(_redisMock.Object, _optionsMock.Object, _loggerMock.Object);

        // Act
        var result = await sut.AcquireAsync("user@example.com", "OrderCreated", NotificationChannel.Email);

        // Assert
        result.Allowed.Should().BeTrue();
    }

    #endregion

    #region RateLimitResult

    [Fact]
    public void RateLimitResult_AllowedResult_ShouldSetAllowedTrue()
    {
        var result = RateLimitResult.AllowedResult();
        result.Allowed.Should().BeTrue();
        result.ErrorCode.Should().BeNull();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void RateLimitResult_DeniedResult_ShouldSetCorrectProperties()
    {
        var resetAt = DateTime.UtcNow.AddHours(1);
        var result = RateLimitResult.DeniedResult("RATE_LIMITED", "Too many requests", 11, 10, resetAt);

        result.Allowed.Should().BeFalse();
        result.ErrorCode.Should().Be("RATE_LIMITED");
        result.ErrorMessage.Should().Be("Too many requests");
        result.CurrentCount.Should().Be(11);
        result.Limit.Should().Be(10);
        result.ResetAt.Should().Be(resetAt);
    }

    #endregion
}