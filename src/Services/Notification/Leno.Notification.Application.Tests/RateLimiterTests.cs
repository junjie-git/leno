using Leno.Notification.Domain.Services;
using Leno.Notification.Domain.ValueObjects;
using Leno.Notification.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;

namespace Leno.Notification.Application.Tests;

public class RateLimiterTests
{
    private readonly Mock<IConnectionMultiplexer> _redisMock = new();
    private readonly Mock<ILogger<RedisRateLimiter>> _loggerMock = new();
    private readonly Mock<IDatabase> _databaseMock = new();

    private RedisRateLimiter CreateSut()
    {
        _redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_databaseMock.Object);
        return new RedisRateLimiter(_redisMock.Object, _loggerMock.Object);
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
        var sut = new RedisRateLimiter(_redisMock.Object, _loggerMock.Object);

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