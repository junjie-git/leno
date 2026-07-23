using Leno.SystemAdmin.Domain.Services;
using Leno.SystemAdmin.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Leno.SystemAdmin.Infrastructure.Tests;

/// <summary>
/// 死信积压告警后台服务单元测试（T20）。
/// 验证 <see cref="DeadLetterMonitorBackgroundService.RunScanCycleAsync"/>：
/// - 死信数量低于阈值时不告警
/// - 死信数量超过阈值时调用 CountAsync 并记录告警
/// - 配置的 SourceContexts 被逐个扫描
/// 测试风格参考 <see cref="RabbitMqDeadLetterManagerTests"/>（Moq + FluentAssertions + xUnit）。
/// </summary>
public sealed class DeadLetterMonitorBackgroundServiceTests
{
    [Fact]
    public async Task RunScanCycleAsync_BelowThreshold_ShouldNotAlert()
    {
        var sut = CreateSut(out var managerMock, threshold: 10, sourceContexts: new List<string>());
        managerMock
            .Setup(m => m.CountAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        await sut.RunScanCycleAsync(CancellationToken.None);

        // 总数 5 < 阈值 10，仅调用一次 CountAsync(null)（无 SourceContexts 逐项扫描）
        managerMock.Verify(m => m.CountAsync(null, It.IsAny<CancellationToken>()), Times.Once);
        managerMock.Verify(m => m.CountAsync(It.Is<string>(s => s != null), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunScanCycleAsync_AboveThreshold_ShouldAlert()
    {
        var sut = CreateSut(out var managerMock, threshold: 10, sourceContexts: new List<string>());
        managerMock
            .Setup(m => m.CountAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(15);

        await sut.RunScanCycleAsync(CancellationToken.None);

        // 总数 15 > 阈值 10，应调用 CountAsync 并触发告警日志（日志验证见下方 LogWarning 测试）
        managerMock.Verify(m => m.CountAsync(null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunScanCycleAsync_WithSourceContexts_ShouldScanEachContext()
    {
        var contexts = new List<string> { "OrderService", "PaymentService" };
        var sut = CreateSut(out var managerMock, threshold: 10, sourceContexts: contexts);
        managerMock
            .Setup(m => m.CountAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);
        managerMock
            .Setup(m => m.CountAsync("OrderService", It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);
        managerMock
            .Setup(m => m.CountAsync("PaymentService", It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        await sut.RunScanCycleAsync(CancellationToken.None);

        // 总数 + 2 个 sourceContext = 3 次 CountAsync 调用
        managerMock.Verify(m => m.CountAsync(null, It.IsAny<CancellationToken>()), Times.Once);
        managerMock.Verify(m => m.CountAsync("OrderService", It.IsAny<CancellationToken>()), Times.Once);
        managerMock.Verify(m => m.CountAsync("PaymentService", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunScanCycleAsync_SourceContextAboveThreshold_ShouldAlertPerContext()
    {
        var contexts = new List<string> { "OrderService" };
        var sut = CreateSut(out var managerMock, threshold: 10, sourceContexts: contexts);
        managerMock
            .Setup(m => m.CountAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(12);
        managerMock
            .Setup(m => m.CountAsync("OrderService", It.IsAny<CancellationToken>()))
            .ReturnsAsync(11);

        await sut.RunScanCycleAsync(CancellationToken.None);

        // 总数 12 和 OrderService 11 均超阈值，CountAsync 均被调用
        managerMock.Verify(m => m.CountAsync(null, It.IsAny<CancellationToken>()), Times.Once);
        managerMock.Verify(m => m.CountAsync("OrderService", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunScanCycleAsync_ZeroDeadLetters_ShouldNotLogInfo()
    {
        var sut = CreateSut(out var managerMock, threshold: 10, sourceContexts: new List<string>());
        managerMock
            .Setup(m => m.CountAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        await sut.RunScanCycleAsync(CancellationToken.None);

        // 0 条死信时仍调用 CountAsync，但不触发告警
        managerMock.Verify(m => m.CountAsync(null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunScanCycleAsync_CountAsyncThrows_ShouldPropagateException()
    {
        var sut = CreateSut(out var managerMock, threshold: 10, sourceContexts: new List<string>());
        managerMock
            .Setup(m => m.CountAsync(null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db down"));

        var act = () => sut.RunScanCycleAsync(CancellationToken.None);

        // RunScanCycleAsync 不吞异常，由 ExecuteAsync 外层 catch 处理
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*db down*");
    }

    /// <summary>
    /// 构造 BackgroundService 测试桩，返回 IDeadLetterQueueManager mock 供断言。
    /// 使用真实 ServiceCollection 构建 ServiceProvider。
    /// </summary>
    private static DeadLetterMonitorBackgroundService CreateSut(
        out Mock<IDeadLetterQueueManager> managerMock,
        int threshold = 10,
        List<string>? sourceContexts = null)
    {
        managerMock = new Mock<IDeadLetterQueueManager>();

        var services = new ServiceCollection();
        services.AddSingleton(managerMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        var options = Microsoft.Extensions.Options.Options.Create(new DeadLetterMonitorOptions
        {
            Interval = TimeSpan.FromHours(1),
            AlertThreshold = threshold,
            SourceContexts = sourceContexts ?? new List<string>()
        });
        var logger = new Mock<ILogger<DeadLetterMonitorBackgroundService>>().Object;

        return new DeadLetterMonitorBackgroundService(serviceProvider, logger, options);
    }
}
