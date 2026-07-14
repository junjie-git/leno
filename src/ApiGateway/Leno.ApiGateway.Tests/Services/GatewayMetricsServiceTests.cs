using Leno.ApiGateway.Services;
using Prometheus;

namespace Leno.ApiGateway.Tests.Services;

public class GatewayMetricsServiceTests : IDisposable
{
    private readonly CollectorRegistry _registry;
    private readonly GatewayMetricsService _service;

    public GatewayMetricsServiceTests()
    {
        // 每个测试使用独立的 CollectorRegistry，避免全局注册冲突
        _registry = new CollectorRegistry();
        _service = new GatewayMetricsService(_registry);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void RecordRequest_IncrementsRequestsTotalCounter()
    {
        // Act
        _service.RecordRequest(route: "product", method: "GET", statusCode: 200);

        // Assert
        var counter = _registry.GetSingleValue("gateway_requests_total",
            "route", "product", "method", "GET", "status_code", "200");
        counter.Should().Be(1);
    }

    [Fact]
    public void RecordRequest_MultipleTimes_AccumulatesCount()
    {
        // Act
        _service.RecordRequest("order", "POST", 201);
        _service.RecordRequest("order", "POST", 201);
        _service.RecordRequest("order", "POST", 500);

        // Assert
        var successCount = _registry.GetSingleValue("gateway_requests_total",
            "route", "order", "method", "POST", "status_code", "201");
        successCount.Should().Be(2);

        var errorCount = _registry.GetSingleValue("gateway_requests_total",
            "route", "order", "method", "POST", "status_code", "500");
        errorCount.Should().Be(1);
    }

    [Fact]
    public void RecordRequestDuration_ObservesHistogram()
    {
        // Act
        _service.RecordRequestDuration(route: "product", method: "GET", durationMs: 125);

        // Assert — Histogram 的 _count 应为 1
        var count = _registry.GetSingleValue("gateway_request_duration_count",
            "route", "product", "method", "GET");
        count.Should().Be(1);

        var sum = _registry.GetSingleValue("gateway_request_duration_sum",
            "route", "product", "method", "GET");
        sum.Should().Be(125);
    }

    [Fact]
    public void IncrementActiveRequests_IncrementsGauge()
    {
        // Act
        _service.IncrementActiveRequests();
        _service.IncrementActiveRequests();

        // Assert
        var value = _registry.GetSingleValue("gateway_active_requests");
        value.Should().Be(2);
    }

    [Fact]
    public void DecrementActiveRequests_DecrementsGauge()
    {
        // Arrange
        _service.IncrementActiveRequests();
        _service.IncrementActiveRequests();

        // Act
        _service.DecrementActiveRequests();

        // Assert
        var value = _registry.GetSingleValue("gateway_active_requests");
        value.Should().Be(1);
    }

    [Fact]
    public void SetCircuitBreakerState_UpdatesGaugeValue()
    {
        // Act
        _service.SetCircuitBreakerState(cluster: "order", isOpen: true);

        // Assert — open=1
        var openValue = _registry.GetSingleValue("gateway_circuit_breaker_state",
            "cluster", "order");
        openValue.Should().Be(1);

        // Act — 恢复 closed
        _service.SetCircuitBreakerState("order", isOpen: false);

        // Assert — closed=0
        var closedValue = _registry.GetSingleValue("gateway_circuit_breaker_state",
            "cluster", "order");
        closedValue.Should().Be(0);
    }

    [Fact]
    public void RecordRateLimitRejection_IncrementsCounter()
    {
        // Act
        _service.RecordRateLimitRejection(route: "seckill", policy: "seckill-policy");
        _service.RecordRateLimitRejection(route: "seckill", policy: "seckill-policy");

        // Assert
        var value = _registry.GetSingleValue("gateway_rate_limit_rejected",
            "route", "seckill", "policy", "seckill-policy");
        value.Should().Be(2);
    }

    [Fact]
    public void RecordBlacklistHit_IncrementsCounter()
    {
        // Act
        _service.RecordBlacklistHit();
        _service.RecordBlacklistHit();
        _service.RecordBlacklistHit();

        // Assert
        var value = _registry.GetSingleValue("gateway_blacklist_hits");
        value.Should().Be(3);
    }

    [Fact]
    public void RecordRequest_WithNullRoute_UsesEmptyString()
    {
        // Act — 健康检查等未路由到 YARP 的请求 route 为 null
        _service.RecordRequest(route: null, method: "GET", statusCode: 200);

        // Assert
        var value = _registry.GetSingleValue("gateway_requests_total",
            "route", "", "method", "GET", "status_code", "200");
        value.Should().Be(1);
    }
}

internal static class CollectorRegistryExtensions
{
    /// <summary>
    /// 从 CollectorRegistry 中读取指定指标 + 标签组合的当前值（适用于 Counter/Gauge/Histogram 的 _count/_sum）。
    /// </summary>
    public static double GetSingleValue(
        this CollectorRegistry registry,
        string metricName,
        params string[] labelValues)
    {
        using var stream = new MemoryStream();
        registry.CollectAndExportAsTextAsync(stream).GetAwaiter().GetResult();
        stream.Position = 0;
        using var reader = new StreamReader(stream);
        var text = reader.ReadToEnd();

        var labelPart = labelValues.Length > 0
            ? "{" + string.Join(",", Enumerable.Range(0, labelValues.Length / 2)
                .Select(i => $"{labelValues[i * 2]}=\"{labelValues[i * 2 + 1]}\"")) + "}"
            : string.Empty;

        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            if (line.StartsWith($"{metricName}{labelPart} ", StringComparison.Ordinal))
            {
                var valueStr = line.Substring(line.LastIndexOf(' ') + 1).Trim();
                return double.Parse(valueStr, System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        throw new InvalidOperationException(
            $"Metric {metricName}{labelPart} not found in registry output. Lines:\n{text}");
    }
}
