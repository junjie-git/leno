using Leno.Infrastructure.Abstractions.Geo;
using Leno.Infrastructure.Abstractions.UserAgent;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Infrastructure.Consumers;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Leno.SystemAdmin.Infrastructure.Tests.Consumers;

/// <summary>
/// 验证 LoginLogConsumer：
/// - 成功登录事件持久化为 Success 日志
/// - 失败登录事件持久化为 Failed 日志并携带 FailureReason
/// - 重复 EventId 幂等跳过
/// - UserAgent 字符串被正确解析为 Browser / Os
/// </summary>
public sealed class LoginLogConsumerTests
{
    private readonly Mock<ILoginLogRepository> _repoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IUserAgentParser> _uaMock = new();
    private readonly Mock<IGeoLocationResolver> _geoMock = new();
    private readonly LoginLogConsumer _consumer;

    public LoginLogConsumerTests()
    {
        _consumer = new LoginLogConsumer(_repoMock.Object, _uowMock.Object, _uaMock.Object, _geoMock.Object, NullLogger<LoginLogConsumer>.Instance);
        _uaMock.Setup(p => p.ParseBrowser(It.IsAny<string>())).Returns("Chrome 120");
        _uaMock.Setup(p => p.ParseOs(It.IsAny<string>())).Returns("Windows 11");
        _uaMock.Setup(p => p.ParseDeviceFingerprint(It.IsAny<string>())).Returns("abc12345");
        _geoMock.Setup(g => g.Resolve(It.IsAny<string>())).Returns(new GeoLocation { Country = "内网", Province = "本地" });
    }

    [Fact]
    public async Task Consume_SuccessEvent_PersistsSuccessLog()
    {
        var evt = new UserLoggedInEvent
        {
            Username = "admin",
            UserId = Guid.NewGuid(),
            IpAddress = "10.0.0.1",
            UserAgent = "Mozilla/5.0",
            TraceId = "trace-1",
            DurationMs = 150,
            Success = true
        };
        var context = CreateContext(evt);
        _repoMock.Setup(r => r.GetByEventIdAsync(evt.EventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LoginLog?)null);

        await _consumer.Consume(context);

        _repoMock.Verify(r => r.AddAsync(It.Is<LoginLog>(l => l.Result == LoginResult.Success && l.FailureReason == null), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Consume_FailedEvent_PersistsFailedLogWithReason()
    {
        var evt = new UserLoggedInEvent
        {
            Username = "admin",
            IpAddress = "10.0.0.1",
            UserAgent = "Mozilla/5.0",
            TraceId = "trace-2",
            DurationMs = 80,
            Success = false,
            FailureReason = "密码错误"
        };
        var context = CreateContext(evt);
        _repoMock.Setup(r => r.GetByEventIdAsync(evt.EventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LoginLog?)null);

        await _consumer.Consume(context);

        _repoMock.Verify(r => r.AddAsync(It.Is<LoginLog>(l => l.Result == LoginResult.Failed && l.FailureReason == "密码错误"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Consume_DuplicateEventId_IdempotentSkip()
    {
        var evt = new UserLoggedInEvent { Username = "admin", Success = true };
        var existing = LoginLog.CreateSuccess(Guid.NewGuid(), "admin", Guid.NewGuid(), "1.1.1.1", "Chrome", "Windows", "UA", "t1", 50, DateTime.UtcNow);
        var context = CreateContext(evt);
        _repoMock.Setup(r => r.GetByEventIdAsync(evt.EventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        await _consumer.Consume(context);

        _repoMock.Verify(r => r.AddAsync(It.IsAny<LoginLog>(), It.IsAny<CancellationToken>()), Times.Never);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Consume_ParsesUserAgent_PopulatesBrowserAndOs()
    {
        var evt = new UserLoggedInEvent
        {
            Username = "admin",
            UserId = Guid.NewGuid(),
            IpAddress = "10.0.0.1",
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/120.0.0.0",
            TraceId = "trace-3",
            DurationMs = 100,
            Success = true
        };
        var context = CreateContext(evt);
        _repoMock.Setup(r => r.GetByEventIdAsync(evt.EventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LoginLog?)null);

        await _consumer.Consume(context);

        _uaMock.Verify(p => p.ParseBrowser(evt.UserAgent), Times.Once);
        _uaMock.Verify(p => p.ParseOs(evt.UserAgent), Times.Once);
        _repoMock.Verify(r => r.AddAsync(It.Is<LoginLog>(l => l.Browser == "Chrome 120" && l.Os == "Windows 11"), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static ConsumeContext<T> CreateContext<T>(T message) where T : class
    {
        var mockContext = new Mock<ConsumeContext<T>>();
        mockContext.SetupGet(c => c.Message).Returns(message);
        mockContext.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return mockContext.Object;
    }
}
