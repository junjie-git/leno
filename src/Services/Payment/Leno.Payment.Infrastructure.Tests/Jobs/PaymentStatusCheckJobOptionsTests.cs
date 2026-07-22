using Leno.Payment.Domain.Aggregates;
using Leno.Payment.Domain.Repositories;
using Leno.Payment.Domain.Services;
using Leno.Payment.Domain.ValueObjects;
using Leno.Payment.Infrastructure.Channels;
using Leno.Payment.Infrastructure.Config;
using Leno.Payment.Infrastructure.Jobs;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Leno.Payment.Infrastructure.Tests.Jobs;

/// <summary>
/// P2-20 测试：验证 PaymentStatusCheckJob 从 IOptions&lt;PaymentJobOptions&gt; 读取 BatchSize/ThresholdMinutes，
/// 覆盖原硬编码常量（ThresholdMinutes=5，BatchSize=100）。
/// 修复前：第 19-20 行 <c>private const int ThresholdMinutes = 5; private const int BatchSize = 100;</c> 硬编码不可配置。
/// 修复后：构造函数注入 IOptions&lt;PaymentJobOptions&gt;，ExecuteAsync/CloseExpiredOrdersAsync 使用 _jobOptions.ThresholdMinutes/BatchSize。
/// </summary>
public class PaymentStatusCheckJobOptionsTests
{
    /// <summary>
    /// 构造 job，注入指定的 PaymentJobOptions。传 null 模拟未提供配置（回退到默认值）。
    /// </summary>
    private static PaymentStatusCheckJob CreateJob(
        Mock<IPaymentOrderRepository> repoMock,
        Mock<IUnitOfWork> uowMock,
        Mock<IPaymentChannelFactory> factoryMock,
        PaymentJobOptions? options)
    {
        IOptions<PaymentJobOptions>? optionsWrapper = options is null
            ? null
            : Options.Create(options);

        return new PaymentStatusCheckJob(
            repoMock.Object,
            uowMock.Object,
            factoryMock.Object,
            NullLogger<PaymentStatusCheckJob>.Instance,
            optionsWrapper);
    }

    /// <summary>
    /// 配置 repoMock 使 QueryAsync 返回空列表并捕获实际传入的 endDate（threshold）与 pageSize。
    /// </summary>
    private static void SetupQueryAsyncCapture(
        Mock<IPaymentOrderRepository> repoMock,
        List<DateTime?> capturedEndDates,
        List<int> capturedPageSizes)
    {
        repoMock
            .Setup(r => r.QueryAsync(
                It.IsAny<Guid?>(),
                It.IsAny<PaymentChannel?>(),
                It.IsAny<PaymentStatus?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Callback<Guid?, PaymentChannel?, PaymentStatus?, DateTime?, DateTime?, int, int, CancellationToken>(
                (_, _, _, _, endDate, _, pageSize, _) =>
                {
                    capturedEndDates.Add(endDate);
                    capturedPageSizes.Add(pageSize);
                })
            .ReturnsAsync(new List<PaymentOrder>());
    }

    [Fact]
    public async Task ExecuteAsync_WithConfiguredBatchSize_ShouldPassConfiguredValueToQueryAsync()
    {
        // 安排：配置 BatchSize=50（覆盖默认 100），ThresholdMinutes 保持默认 5
        var options = new PaymentJobOptions { BatchSize = 50, ThresholdMinutes = 5 };

        var repoMock = new Mock<IPaymentOrderRepository>();
        var capturedPageSizes = new List<int>();
        var capturedEndDates = new List<DateTime?>();
        SetupQueryAsyncCapture(repoMock, capturedEndDates, capturedPageSizes);

        // 过期关单分支返回空，避免干扰
        repoMock
            .Setup(r => r.GetExpiredOrdersAsync(
                It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PaymentOrder>());

        var uowMock = new Mock<IUnitOfWork>();
        var adapterMock = new Mock<IPaymentChannelAdapter>();
        var factoryMock = new Mock<IPaymentChannelFactory>();
        factoryMock.Setup(f => f.GetAdapter(It.IsAny<PaymentChannel>())).Returns(adapterMock.Object);

        var sut = CreateJob(repoMock, uowMock, factoryMock, options);

        // 行动
        await sut.ExecuteAsync(CancellationToken.None);

        // 断言：QueryAsync 被调用两次（Pending + ChannelOrdered），每次 pageSize 均为配置值 50
        Assert.Equal(2, capturedPageSizes.Count);
        Assert.All(capturedPageSizes, ps => Assert.Equal(50, ps));
    }

    [Fact]
    public async Task ExecuteAsync_WithConfiguredThresholdMinutes_ShouldComputeThresholdFromConfig()
    {
        // 安排：配置 ThresholdMinutes=10（覆盖默认 5），BatchSize 保持默认 100
        var options = new PaymentJobOptions { BatchSize = 100, ThresholdMinutes = 10 };

        var repoMock = new Mock<IPaymentOrderRepository>();
        var capturedPageSizes = new List<int>();
        var capturedEndDates = new List<DateTime?>();
        SetupQueryAsyncCapture(repoMock, capturedEndDates, capturedPageSizes);

        repoMock
            .Setup(r => r.GetExpiredOrdersAsync(
                It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PaymentOrder>());

        var uowMock = new Mock<IUnitOfWork>();
        var adapterMock = new Mock<IPaymentChannelAdapter>();
        var factoryMock = new Mock<IPaymentChannelFactory>();
        factoryMock.Setup(f => f.GetAdapter(It.IsAny<PaymentChannel>())).Returns(adapterMock.Object);

        var sut = CreateJob(repoMock, uowMock, factoryMock, options);
        var executionStart = DateTime.UtcNow;

        // 行动
        await sut.ExecuteAsync(CancellationToken.None);

        // 断言：threshold 应为 UtcNow.AddMinutes(-10)，与执行时刻的差值在容差内
        Assert.Equal(2, capturedEndDates.Count);
        var expectedThreshold = executionStart.AddMinutes(-10);
        Assert.All(capturedEndDates, endDate =>
        {
            Assert.NotNull(endDate);
            // 容差 5 秒，覆盖执行期间的时间漂移
            Assert.True(Math.Abs((endDate!.Value - expectedThreshold).TotalSeconds) < 5,
                $"threshold={endDate:O} 期望≈{expectedThreshold:O}（UtcNow.AddMinutes(-10)）");
        });
    }

    [Fact]
    public async Task ExecuteAsync_WithoutOptions_ShouldFallBackToDefaults()
    {
        // 安排：不注入 options（模拟直接构造），应回退到 PaymentJobOptions 默认值
        // BatchSize=100，ThresholdMinutes=5（与原硬编码常量一致）
        var repoMock = new Mock<IPaymentOrderRepository>();
        var capturedPageSizes = new List<int>();
        var capturedEndDates = new List<DateTime?>();
        SetupQueryAsyncCapture(repoMock, capturedEndDates, capturedPageSizes);

        repoMock
            .Setup(r => r.GetExpiredOrdersAsync(
                It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PaymentOrder>());

        var uowMock = new Mock<IUnitOfWork>();
        var adapterMock = new Mock<IPaymentChannelAdapter>();
        var factoryMock = new Mock<IPaymentChannelFactory>();
        factoryMock.Setup(f => f.GetAdapter(It.IsAny<PaymentChannel>())).Returns(adapterMock.Object);

        var sut = CreateJob(repoMock, uowMock, factoryMock, options: null);
        var executionStart = DateTime.UtcNow;

        // 行动
        await sut.ExecuteAsync(CancellationToken.None);

        // 断言：回退默认 BatchSize=100，ThresholdMinutes=5
        Assert.Equal(2, capturedPageSizes.Count);
        Assert.All(capturedPageSizes, ps => Assert.Equal(100, ps));

        var expectedThreshold = executionStart.AddMinutes(-5);
        Assert.All(capturedEndDates, endDate =>
        {
            Assert.NotNull(endDate);
            Assert.True(Math.Abs((endDate!.Value - expectedThreshold).TotalSeconds) < 5,
                $"threshold={endDate:O} 期望≈{expectedThreshold:O}（UtcNow.AddMinutes(-5)）");
        });
    }

    [Fact]
    public async Task ExecuteAsync_WithConfiguredBatchSize_ShouldPassConfiguredValueToGetExpiredOrdersAsync()
    {
        // 安排：配置 BatchSize=25，验证过期关单分页查询同样使用配置值
        var options = new PaymentJobOptions { BatchSize = 25, ThresholdMinutes = 5 };

        var repoMock = new Mock<IPaymentOrderRepository>();
        // QueryAsync 返回空，避免触发渠道查询分支
        repoMock
            .Setup(r => r.QueryAsync(
                It.IsAny<Guid?>(),
                It.IsAny<PaymentChannel?>(),
                It.IsAny<PaymentStatus?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PaymentOrder>());

        var capturedExpiredPageSizes = new List<int>();
        repoMock
            .Setup(r => r.GetExpiredOrdersAsync(
                It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<DateTime, int, int, CancellationToken>((_, _, pageSize, _) =>
                capturedExpiredPageSizes.Add(pageSize))
            .ReturnsAsync(new List<PaymentOrder>());

        var uowMock = new Mock<IUnitOfWork>();
        var adapterMock = new Mock<IPaymentChannelAdapter>();
        var factoryMock = new Mock<IPaymentChannelFactory>();
        factoryMock.Setup(f => f.GetAdapter(It.IsAny<PaymentChannel>())).Returns(adapterMock.Object);

        var sut = CreateJob(repoMock, uowMock, factoryMock, options);

        // 行动
        await sut.ExecuteAsync(CancellationToken.None);

        // 断言：GetExpiredOrdersAsync 至少调用一次（第 1 页），pageSize 为配置值 25
        Assert.NotEmpty(capturedExpiredPageSizes);
        Assert.All(capturedExpiredPageSizes, ps => Assert.Equal(25, ps));
    }
}
