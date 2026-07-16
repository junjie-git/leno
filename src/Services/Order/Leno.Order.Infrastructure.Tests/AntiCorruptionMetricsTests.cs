using System.Diagnostics.Metrics;
using Leno.Infrastructure.Auth;
using Leno.Order.Domain.Exceptions;
using Leno.Order.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System.Net;

namespace Leno.Order.Infrastructure.Tests;

/// <summary>
/// 防腐层降级告警测试（T17）。
/// 验证：
/// - 远程失败时 Prometheus 指标 <c>anticorruption_failure_total{service,operation}</c> 计数器递增（按 service/operation 维度）。
/// - 结构化日志包含 <c>Service</c>/<c>Operation</c> 字段。
/// - 成功调用不递增失败计数器。
/// 测试使用 <see cref="MeterListener"/> 捕获计数器增量，避免依赖 Prometheus exporter。
/// </summary>
public class AntiCorruptionMetricsTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();

    [Theory]
    [InlineData("Freeze", "points", "freeze")]
    [InlineData("Confirm", "points", "confirm")]
    [InlineData("Release", "points", "release")]
    public async Task Points_RemoteFailure_ShouldIncrementFailureCounter(string operation, string expectedService, string expectedOperation)
    {
        var captured = new List<(string Service, string Operation, int Delta)>();
        using var listener = CreateMeterListener(captured);

        var service = CreatePointsService(_ => throw new HttpRequestException("connection refused"));

        var act = () => operation switch
        {
            "Freeze" => service.FreezeAsync(UserId, OrderId, 100, CancellationToken.None),
            "Confirm" => service.ConfirmDeductionAsync(OrderId, CancellationToken.None),
            "Release" => service.ReleaseAsync(OrderId, CancellationToken.None),
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };

        await act.Should().ThrowAsync<OrderDomainException>();

        captured.Should().ContainSingle()
            .Which.Should().Match<(string Service, string Operation, int Delta)>(
                x => x.Service == expectedService && x.Operation == expectedOperation && x.Delta == 1);
    }

    [Fact]
    public async Task Promotion_CalculateDiscount_RemoteFailure_ShouldIncrementFailureCounter()
    {
        var captured = new List<(string Service, string Operation, int Delta)>();
        using var listener = CreateMeterListener(captured);

        var service = CreatePromotionService(_ => throw new HttpRequestException("network down"));

        var act = () => service.CalculateDiscountAsync(UserId, new List<(Guid, decimal)> { (Guid.NewGuid(), 10m) }, CancellationToken.None);

        await act.Should().ThrowAsync<OrderDomainException>();

        captured.Should().ContainSingle()
            .Which.Should().Match<(string Service, string Operation, int Delta)>(
                x => x.Service == "promotion" && x.Operation == "calculate_discount" && x.Delta == 1);
    }

    [Fact]
    public async Task Promotion_ReleaseCoupons_NonSuccessStatusCode_ShouldIncrementFailureCounter()
    {
        var captured = new List<(string Service, string Operation, int Delta)>();
        using var listener = CreateMeterListener(captured);

        var service = CreatePromotionService(_ => Response(HttpStatusCode.InternalServerError));

        var act = () => service.ReleaseCouponsAsync(OrderId, CancellationToken.None);

        await act.Should().ThrowAsync<OrderDomainException>();

        captured.Should().ContainSingle()
            .Which.Should().Match<(string Service, string Operation, int Delta)>(
                x => x.Service == "promotion" && x.Operation == "release_coupons" && x.Delta == 1);
    }

    [Fact]
    public async Task Promotion_LockCoupon_RemoteFailure_ShouldIncrementFailureCounter()
    {
        var captured = new List<(string Service, string Operation, int Delta)>();
        using var listener = CreateMeterListener(captured);

        var service = CreatePromotionService(_ => throw new HttpRequestException("network down"));

        var act = () => service.LockCouponAsync(UserId, Guid.NewGuid(), OrderId, CancellationToken.None);

        await act.Should().ThrowAsync<OrderDomainException>();

        captured.Should().ContainSingle()
            .Which.Should().Match<(string Service, string Operation, int Delta)>(
                x => x.Service == "promotion" && x.Operation == "lock_coupon" && x.Delta == 1);
    }

    [Fact]
    public async Task Points_Success_ShouldNotIncrementFailureCounter()
    {
        var captured = new List<(string Service, string Operation, int Delta)>();
        using var listener = CreateMeterListener(captured);

        var service = CreatePointsService(_ => Response(HttpStatusCode.OK));

        await service.FreezeAsync(UserId, OrderId, 100, CancellationToken.None);

        captured.Should().BeEmpty();
    }

    [Fact]
    public async Task Promotion_CalculateDiscount_Success_ShouldNotIncrementFailureCounter()
    {
        var captured = new List<(string Service, string Operation, int Delta)>();
        using var listener = CreateMeterListener(captured);

        var service = CreatePromotionService(_ => Json(new { data = new { totalDiscountAmount = 5m, currency = "CNY" } }));

        var result = await service.CalculateDiscountAsync(UserId, new List<(Guid, decimal)> { (Guid.NewGuid(), 100m) }, CancellationToken.None);

        result.Should().Be(5m);
        captured.Should().BeEmpty();
    }

    [Fact]
    public async Task Points_FreezeeFailure_ShouldLogStructuredServiceOperationFields()
    {
        // 验证结构化日志包含 Service/Operation 字段（T17 要求）
        var loggerMock = new Mock<ILogger<PointsAntiCorruptionService>>();
        var service = new PointsAntiCorruptionService(
            new HttpClient(new FakeHandler(_ => throw new HttpRequestException("down"))) { BaseAddress = new Uri("http://test/") },
            Options.Create(new InternalApiKeyOptions()),
            loggerMock.Object);

        var act = () => service.FreezeAsync(UserId, OrderId, 100, CancellationToken.None);

        await act.Should().ThrowAsync<OrderDomainException>();

        // 验证 LogError 被调用，且 message template 含 Service/Operation 占位符
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Service") && v.ToString()!.Contains("Operation")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    // ---- Helpers ----

    private static MeterListener CreateMeterListener(List<(string Service, string Operation, int Delta)> captured)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == AntiCorruptionMetrics.MeterName
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

    private static PointsAntiCorruptionService CreatePointsService(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
    {
        var http = new HttpClient(new FakeHandler(handler)) { BaseAddress = new Uri("http://test/") };
        var options = Options.Create(new InternalApiKeyOptions());
        var logger = NullLogger<PointsAntiCorruptionService>.Instance;
        return new PointsAntiCorruptionService(http, options, logger);
    }

    private static PromotionAntiCorruptionService CreatePromotionService(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
    {
        var http = new HttpClient(new FakeHandler(handler)) { BaseAddress = new Uri("http://test/") };
        var options = Options.Create(new InternalApiKeyOptions());
        var logger = NullLogger<PromotionAntiCorruptionService>.Instance;
        return new PromotionAntiCorruptionService(http, options, logger);
    }

    private static Task<HttpResponseMessage> Response(HttpStatusCode code)
        => Task.FromResult<HttpResponseMessage>(new HttpResponseMessage(code));

    private static Task<HttpResponseMessage> Json(object payload)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(payload),
                System.Text.Encoding.UTF8,
                "application/json")
        };
        return Task.FromResult(response);
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

        public FakeHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => _handler(request);
    }
}
