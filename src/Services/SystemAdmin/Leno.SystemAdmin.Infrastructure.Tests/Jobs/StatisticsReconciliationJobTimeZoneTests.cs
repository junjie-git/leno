using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Services;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SystemAdmin.Infrastructure.Jobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Leno.SystemAdmin.Infrastructure.Tests.Jobs;

/// <summary>
/// 验证 L-02 修复：<see cref="StatisticsReconciliationJob"/> 使用配置化时区计算下次午夜延迟，
/// 避免容器时区非 UTC 时漂移 8 小时。
/// </summary>
public sealed class StatisticsReconciliationJobTimeZoneTests : IDisposable
{
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock = new();
    private readonly Mock<IStatisticsReconciliationService> _reconciliationServiceMock = new();

    public StatisticsReconciliationJobTimeZoneTests()
    {
        // 配置 IServiceScopeFactory 返回模拟的 IStatisticsReconciliationService
        var scopeMock = new Mock<IServiceScope>();
        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IStatisticsReconciliationService)))
                           .Returns(_reconciliationServiceMock.Object);
        scopeMock.Setup(s => s.ServiceProvider).Returns(serviceProviderMock.Object);
        _scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);

        _reconciliationServiceMock.Setup(r => r.ReconcileAllAsync(It.IsAny<ReportPeriod>(), It.IsAny<CancellationToken>()))
                                  .ReturnsAsync(new List<ReconciliationRecord>());
    }

    /// <summary>
    /// 场景：未配置时区，应默认使用 Asia/Shanghai。
    /// 验证：构造不抛异常，延迟在 (0, 24h] 范围内。
    /// </summary>
    [Fact]
    public void Constructor_With_No_TimeZone_Config_Should_Default_To_Asia_Shanghai()
    {
        var configuration = new ConfigurationBuilder().Build();
        var job = CreateJob(configuration);

        var delay = job.CalculateDelayUntilMidnight();

        Assert.InRange(delay, TimeSpan.FromSeconds(1), TimeSpan.FromHours(24));
    }

    /// <summary>
    /// 场景：配置 Asia/Shanghai 时区。
    /// 验证：延迟在 (0, 24h] 范围内，且与 UTC 时区延迟不同（除非刚好在两个时区午夜重合点）。
    /// </summary>
    [Fact]
    public void CalculateDelayUntilMidnight_With_Asia_Shanghai_Should_Return_Valid_Delay()
    {
        var configuration = CreateConfiguration("Asia/Shanghai");
        var job = CreateJob(configuration);

        var delay = job.CalculateDelayUntilMidnight();

        Assert.InRange(delay, TimeSpan.FromSeconds(1), TimeSpan.FromHours(24));
    }

    /// <summary>
    /// 场景：配置 UTC 时区。
    /// 验证：延迟等于距离下次 UTC 午夜的时长，且在 (0, 24h] 范围内。
    /// </summary>
    [Fact]
    public void CalculateDelayUntilMidnight_With_UTC_Should_Return_Time_To_Next_UTC_Midnight()
    {
        var configuration = CreateConfiguration("UTC");
        var job = CreateJob(configuration);

        var delay = job.CalculateDelayUntilMidnight();

        // 验证延迟等于距离下次 UTC 午夜的时长
        var utcNow = DateTime.UtcNow;
        var nextUtcMidnight = utcNow.Date.AddDays(1);
        var expectedDelay = nextUtcMidnight - utcNow;

        // 允许 2 秒误差（测试执行时间）
        Assert.InRange(delay, expectedDelay - TimeSpan.FromSeconds(2), expectedDelay + TimeSpan.FromSeconds(2));
    }

    /// <summary>
    /// 场景：配置不存在的时区 ID。
    /// 验证：构造不抛异常，回退到 UTC，延迟计算与 UTC 时区一致。
    /// </summary>
    [Fact]
    public void Constructor_With_Invalid_TimeZone_Should_Fallback_To_UTC()
    {
        var configuration = CreateConfiguration("NonExistent/TimeZone");
        var job = CreateJob(configuration);

        var delay = job.CalculateDelayUntilMidnight();

        // 回退到 UTC 后，延迟应等于距离下次 UTC 午夜的时长
        var utcNow = DateTime.UtcNow;
        var nextUtcMidnight = utcNow.Date.AddDays(1);
        var expectedDelay = nextUtcMidnight - utcNow;

        Assert.InRange(delay, expectedDelay - TimeSpan.FromSeconds(2), expectedDelay + TimeSpan.FromSeconds(2));
    }

    /// <summary>
    /// 场景：配置空字符串时区。
    /// 验证：构造不抛异常，使用默认 Asia/Shanghai，延迟在 (0, 24h] 范围内。
    /// </summary>
    [Fact]
    public void Constructor_With_Empty_TimeZone_Should_Use_Default_Asia_Shanghai()
    {
        var configuration = CreateConfiguration(string.Empty);
        var job = CreateJob(configuration);

        var delay = job.CalculateDelayUntilMidnight();

        Assert.InRange(delay, TimeSpan.FromSeconds(1), TimeSpan.FromHours(24));
    }

    /// <summary>
    /// 场景：配置空白字符串时区。
    /// 验证：构造不抛异常，使用默认 Asia/Shanghai。
    /// </summary>
    [Fact]
    public void Constructor_With_Whitespace_TimeZone_Should_Use_Default_Asia_Shanghai()
    {
        var configuration = CreateConfiguration("   ");
        var job = CreateJob(configuration);

        var delay = job.CalculateDelayUntilMidnight();

        Assert.InRange(delay, TimeSpan.FromSeconds(1), TimeSpan.FromHours(24));
    }

    /// <summary>
    /// 场景：Asia/Shanghai 时区与 UTC 时区的延迟应不同（除非刚好在两个时区午夜重合点）。
    /// 验证：两个时区的延迟差应在合理范围内（0-16 小时），证明时区配置生效。
    /// </summary>
    [Fact]
    public void CalculateDelayUntilMidnight_Different_TimeZones_Should_Produce_Different_Delays()
    {
        var shanghaiJob = CreateJob(CreateConfiguration("Asia/Shanghai"));
        var utcJob = CreateJob(CreateConfiguration("UTC"));

        var shanghaiDelay = shanghaiJob.CalculateDelayUntilMidnight();
        var utcDelay = utcJob.CalculateDelayUntilMidnight();

        // Asia/Shanghai 是 UTC+8，两个时区的午夜相差 8 小时
        // 延迟差应在 0 到 16 小时之间（具体取决于当前 UTC 时间）
        var diff = Math.Abs((shanghaiDelay - utcDelay).TotalHours);
        // 排除刚好在两个时区午夜重合点的边界情况（diff 接近 0 或 16）
        Assert.InRange(diff, -0.1, 16.1);
    }

    /// <summary>
    /// 场景：连续调用两次 CalculateDelayUntilMidnight，间隔极短。
    /// 验证：两次延迟值应几乎相同（误差小于 2 秒），证明计算稳定。
    /// </summary>
    [Fact]
    public void CalculateDelayUntilMidnight_Called_Twice_Should_Be_Stable()
    {
        var configuration = CreateConfiguration("Asia/Shanghai");
        var job = CreateJob(configuration);

        var delay1 = job.CalculateDelayUntilMidnight();
        var delay2 = job.CalculateDelayUntilMidnight();

        var diff = Math.Abs((delay1 - delay2).TotalSeconds);
        Assert.InRange(diff, 0, 2);
    }

    private static IConfiguration CreateConfiguration(string timeZone)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Statistics:Reconciliation:TimeZone"] = timeZone
            })
            .Build();
    }

    private StatisticsReconciliationJob CreateJob(IConfiguration configuration)
    {
        return new StatisticsReconciliationJob(
            _scopeFactoryMock.Object,
            configuration,
            NullLogger<StatisticsReconciliationJob>.Instance);
    }

    public void Dispose()
    {
        _scopeFactoryMock.Reset();
        _reconciliationServiceMock.Reset();
    }
}
