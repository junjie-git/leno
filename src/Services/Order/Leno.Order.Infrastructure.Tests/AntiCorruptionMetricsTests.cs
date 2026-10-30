using System.Diagnostics.Metrics;
using Grpc.Core;
using Leno.Infrastructure.AntiCorruption;
using Leno.Order.Infrastructure.Services.Grpc;
using Leno.SharedContracts.Grpc.Points.V1;
using Leno.SharedContracts.Grpc.Promotion.V1;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Leno.Order.Infrastructure.Tests;

/// <summary>
/// 防腐层降级告警测试（T17 / M4.1）。
/// 验证：
/// - 远程失败时 Prometheus 指标 <c>anticorruption_failure_total{service,operation}</c> 计数器递增（按 service/operation 维度）。
/// - 成功调用不递增失败计数器。
/// 测试使用 <see cref="MeterListener"/> 捕获计数器增量，避免依赖 Prometheus exporter。
/// 双轨下线（B2 Task 5）：HttpClient 实现已删除，用例改写为 gRPC 单轨语义
/// （模拟 RpcException 远程故障，埋点位于 <c>GrpcAntiCorruptionClientBase.ExecuteAsync</c>）。
/// </summary>
[Collection(AntiCorruptionMetricsCollection.Name)]
public class AntiCorruptionMetricsTests
{
    // 静态 Meter 是进程级共享资源，其他类的 ACL 调用与本项目例并发运行时
    // 会导致 captured 串扰（run #10 实证），DisableParallelization 排他运行是根因修复。
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();

    [Theory]
    [InlineData("Freeze", "points", "freeze_points")]
    [InlineData("Confirm", "points", "confirm_deduction")]
    [InlineData("Release", "points", "release_points")]
    public async Task Points_RemoteFailure_ShouldIncrementFailureCounter(string operation, string expectedService, string expectedOperation)
    {
        var captured = new List<(string Service, string Operation, int Delta)>();
        using var listener = CreateMeterListener(captured);

        var clientMock = new Mock<PointsInternalService.PointsInternalServiceClient>();
        var rpcEx = new RpcException(new Status(StatusCode.Unavailable, "connection refused"));
        switch (operation)
        {
            case "Freeze":
                clientMock.Setup(c => c.FreezeAsync(
                        It.IsAny<FreezeRequest>(), It.IsAny<Metadata>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
                    .Throws(rpcEx);
                break;
            case "Confirm":
                clientMock.Setup(c => c.ConfirmAsync(
                        It.IsAny<ConfirmRequest>(), It.IsAny<Metadata>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
                    .Throws(rpcEx);
                break;
            case "Release":
                clientMock.Setup(c => c.ReleaseAsync(
                        It.IsAny<ReleaseRequest>(), It.IsAny<Metadata>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
                    .Throws(rpcEx);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(operation));
        }

        var service = CreatePointsClient(clientMock.Object);

        var act = () => operation switch
        {
            "Freeze" => service.FreezeAsync(UserId, OrderId, 100, CancellationToken.None),
            "Confirm" => service.ConfirmDeductionAsync(OrderId, CancellationToken.None),
            "Release" => service.ReleaseAsync(OrderId, CancellationToken.None),
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };

        var ex = await act.Should().ThrowAsync<AntiCorruptionException>();
        ex.Which.ErrorCode.Should().Be("POINTS_UNAVAILABLE");

        captured.Should().ContainSingle()
            .Which.Should().Match<(string Service, string Operation, int Delta)>(
                x => x.Service == expectedService && x.Operation == expectedOperation && x.Delta == 1);
    }

    [Fact]
    public async Task Promotion_CalculateDiscount_RemoteFailure_ShouldIncrementFailureCounter()
    {
        var captured = new List<(string Service, string Operation, int Delta)>();
        using var listener = CreateMeterListener(captured);

        var clientMock = new Mock<PromotionInternalService.PromotionInternalServiceClient>();
        clientMock.Setup(c => c.CalculateDiscountAsync(
                It.IsAny<CalculateDiscountRequest>(), It.IsAny<Metadata>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Throws(new RpcException(new Status(StatusCode.Unavailable, "network down")));

        var service = CreatePromotionClient(clientMock.Object);

        var act = () => service.CalculateDiscountAsync(UserId, new List<(Guid, decimal)> { (Guid.NewGuid(), 10m) }, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<AntiCorruptionException>();
        ex.Which.ErrorCode.Should().Be("PROMOTION_UNAVAILABLE");

        captured.Should().ContainSingle()
            .Which.Should().Match<(string Service, string Operation, int Delta)>(
                x => x.Service == "promotion" && x.Operation == "calculate_discount" && x.Delta == 1);
    }

    [Fact]
    public async Task Promotion_ReleaseCoupons_BusinessFailure_ShouldIncrementFailureCounter()
    {
        var captured = new List<(string Service, string Operation, int Delta)>();
        using var listener = CreateMeterListener(captured);

        // Internal 属非临时性业务错误（对应原 HTTP 500），同样计入失败指标
        var clientMock = new Mock<PromotionInternalService.PromotionInternalServiceClient>();
        clientMock.Setup(c => c.ReleaseCouponsAsync(
                It.IsAny<ReleaseCouponsRequest>(), It.IsAny<Metadata>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Throws(new RpcException(new Status(StatusCode.Internal, "server error")));

        var service = CreatePromotionClient(clientMock.Object);

        var act = () => service.ReleaseCouponsAsync(OrderId, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<AntiCorruptionException>();
        ex.Which.ErrorCode.Should().Be("PROMOTION_REMOTE_FAILED");

        captured.Should().ContainSingle()
            .Which.Should().Match<(string Service, string Operation, int Delta)>(
                x => x.Service == "promotion" && x.Operation == "release_coupons" && x.Delta == 1);
    }

    [Fact]
    public async Task Promotion_LockCoupon_RemoteFailure_ShouldIncrementFailureCounter()
    {
        var captured = new List<(string Service, string Operation, int Delta)>();
        using var listener = CreateMeterListener(captured);

        var clientMock = new Mock<PromotionInternalService.PromotionInternalServiceClient>();
        clientMock.Setup(c => c.LockCouponAsync(
                It.IsAny<LockCouponRequest>(), It.IsAny<Metadata>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Throws(new RpcException(new Status(StatusCode.Unavailable, "network down")));

        var service = CreatePromotionClient(clientMock.Object);

        var act = () => service.LockCouponAsync(UserId, Guid.NewGuid(), OrderId, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<AntiCorruptionException>();
        ex.Which.ErrorCode.Should().Be("PROMOTION_UNAVAILABLE");

        captured.Should().ContainSingle()
            .Which.Should().Match<(string Service, string Operation, int Delta)>(
                x => x.Service == "promotion" && x.Operation == "lock_coupon" && x.Delta == 1);
    }

    [Fact]
    public async Task Points_Success_ShouldNotIncrementFailureCounter()
    {
        var captured = new List<(string Service, string Operation, int Delta)>();
        using var listener = CreateMeterListener(captured);

        var clientMock = new Mock<PointsInternalService.PointsInternalServiceClient>();
        clientMock.Setup(c => c.FreezeAsync(
                It.IsAny<FreezeRequest>(), It.IsAny<Metadata>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Returns(new AsyncUnaryCall<FreezeResponse>(
                Task.FromResult(new FreezeResponse()),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { }));

        var service = CreatePointsClient(clientMock.Object);

        await service.FreezeAsync(UserId, OrderId, 100, CancellationToken.None);

        // 按维度过滤：只断言本服务无失败计数（防御其他并行来源的条目干扰）
        captured.Where(x => x.Service == "points").Should().BeEmpty();
    }

    [Fact]
    public async Task Promotion_CalculateDiscount_Success_ShouldNotIncrementFailureCounter()
    {
        var captured = new List<(string Service, string Operation, int Delta)>();
        using var listener = CreateMeterListener(captured);

        var clientMock = new Mock<PromotionInternalService.PromotionInternalServiceClient>();
        clientMock.Setup(c => c.CalculateDiscountAsync(
                It.IsAny<CalculateDiscountRequest>(), It.IsAny<Metadata>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Returns(new AsyncUnaryCall<CalculateDiscountResponse>(
                Task.FromResult(new CalculateDiscountResponse { DiscountCents = 500 }),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { }));

        var service = CreatePromotionClient(clientMock.Object);

        var result = await service.CalculateDiscountAsync(UserId, new List<(Guid, decimal)> { (Guid.NewGuid(), 100m) }, CancellationToken.None);

        result.Should().Be(5m);
        // 按维度过滤：只断言本服务无失败计数（防御其他并行来源的条目干扰）
        captured.Where(x => x.Service == "promotion").Should().BeEmpty();
    }

    // ---- Helpers ----

    private static MeterListener CreateMeterListener(List<(string Service, string Operation, int Delta)> captured)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == AntiCorruptionMetrics.Meter.Name
                    && instrument.Name == "anticorruption_failure_total")
                {
                    l.EnableMeasurementEvents(instrument);
                }
            }
        };
        listener.SetMeasurementEventCallback<int>((instrument, value, tags, state) =>
        {
            var tagList = tags;
            string service = "unknown";
            string operation = "unknown";
            for (var i = 0; i < tagList.Length; i++)
            {
                if (tagList[i].Key == AntiCorruptionMetrics.ServiceLabel)
                {
                    service = tagList[i].Value?.ToString() ?? "unknown";
                }
                else if (tagList[i].Key == AntiCorruptionMetrics.OperationLabel)
                {
                    operation = tagList[i].Value?.ToString() ?? "unknown";
                }
            }
            captured.Add((service, operation, value));
        });
        listener.Start();
        return listener;
    }

    private static IOptionsMonitor<AntiCorruptionOptions> CreateOptionsMonitor()
    {
        var mock = new Mock<IOptionsMonitor<AntiCorruptionOptions>>();
        mock.SetupGet(o => o.CurrentValue).Returns(new AntiCorruptionOptions
        {
            TargetInternalApiKeys = new Dictionary<string, string>
            {
                ["Points"] = "test-internal-key",
                ["Promotion"] = "test-internal-key"
            }
        });
        return mock.Object;
    }

    /// <summary>
    /// 构造被测 gRPC 客户端：不注入 IServiceProvider，跳过 Polly retry，
    /// 保证单次故障恰好记录一次失败指标（Delta == 1）。
    /// </summary>
    private static GrpcPointsAntiCorruptionClient CreatePointsClient(PointsInternalService.PointsInternalServiceClient client)
        => new(client, CreateOptionsMonitor(), NullLogger<GrpcPointsAntiCorruptionClient>.Instance);

    private static GrpcPromotionAntiCorruptionClient CreatePromotionClient(PromotionInternalService.PromotionInternalServiceClient client)
        => new(client, CreateOptionsMonitor(), NullLogger<GrpcPromotionAntiCorruptionClient>.Instance);
}
