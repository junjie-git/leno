using Prometheus;

namespace Leno.ApiGateway.Services;

/// <summary>
/// 网关核心 Prometheus 指标服务，集中持有 Spec 6.3 定义的 6 个指标。
/// <para>
/// 通过 <see cref="CollectorRegistry"/> 隔离指标注册，便于单元测试使用独立注册表。
/// 生产环境使用 <c>Metrics.DefaultRegistry</c>（默认构造）。
/// </para>
/// </summary>
public sealed class GatewayMetricsService
{
    // Histogram 桶边界（毫秒）：覆盖 5ms 到 10s 的典型请求耗时范围
    private static readonly double[] DurationBuckets = { 5, 10, 25, 50, 100, 250, 500, 1000, 2500, 5000, 10000 };

    // Histogram 标签列表（CreateHistogram 带 configuration 的重载使用 string[] 而非 params，需静态字段避免 CA1861）
    private static readonly string[] DurationLabels = { "route", "method" };

    private readonly Counter _requestsTotal;
    private readonly Histogram _requestDuration;
    private readonly Gauge _activeRequests;
    private readonly Gauge _circuitBreakerState;
    private readonly Counter _rateLimitRejected;
    private readonly Counter _blacklistHits;

    /// <summary>
    /// 使用默认全局注册表创建实例（生产环境使用）。
    /// </summary>
    public GatewayMetricsService() : this(Metrics.DefaultRegistry)
    {
    }

    /// <summary>
    /// 使用指定注册表创建实例（单元测试使用独立注册表）。
    /// </summary>
    public GatewayMetricsService(CollectorRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        var factory = Metrics.WithCustomRegistry(registry);

        _requestsTotal = factory.CreateCounter(
            "gateway_requests_total",
            "Total number of HTTP requests processed by the gateway.",
            "route", "method", "status_code");

        _requestDuration = factory.CreateHistogram(
            "gateway_request_duration",
            "HTTP request processing duration in milliseconds.",
            DurationLabels,
            new HistogramConfiguration
            {
                Buckets = DurationBuckets
            });

        _activeRequests = factory.CreateGauge(
            "gateway_active_requests",
            "Current number of in-flight requests being processed by the gateway.");

        _circuitBreakerState = factory.CreateGauge(
            "gateway_circuit_breaker_state",
            "Circuit breaker state per cluster (0=closed, 1=open).",
            "cluster");

        _rateLimitRejected = factory.CreateCounter(
            "gateway_rate_limit_rejected",
            "Number of requests rejected by rate limiting.",
            "route", "policy");

        _blacklistHits = factory.CreateCounter(
            "gateway_blacklist_hits",
            "Number of requests rejected because the JWT was on the blacklist.");
    }

    /// <summary>记录一次完整请求（响应已返回）。</summary>
    public void RecordRequest(string? route, string method, int statusCode)
    {
        _requestsTotal.WithLabels(route ?? string.Empty, method, statusCode.ToString()).Inc();
    }

    /// <summary>记录请求耗时分布。</summary>
    public void RecordRequestDuration(string? route, string method, double durationMs)
    {
        _requestDuration.WithLabels(route ?? string.Empty, method).Observe(durationMs);
    }

    /// <summary>请求进入管道时调用，活跃请求数 +1。</summary>
    public void IncrementActiveRequests()
    {
        _activeRequests.Inc();
    }

    /// <summary>请求离开管道时调用，活跃请求数 -1。</summary>
    public void DecrementActiveRequests()
    {
        _activeRequests.Dec();
    }

    /// <summary>更新指定 Cluster 的熔断器状态（0=closed, 1=open）。</summary>
    public void SetCircuitBreakerState(string cluster, bool isOpen)
    {
        _circuitBreakerState.WithLabels(cluster).Set(isOpen ? 1 : 0);
    }

    /// <summary>记录一次限流拒绝事件。</summary>
    public void RecordRateLimitRejection(string? route, string policy)
    {
        _rateLimitRejected.WithLabels(route ?? string.Empty, policy).Inc();
    }

    /// <summary>记录一次黑名单命中事件。</summary>
    public void RecordBlacklistHit()
    {
        _blacklistHits.Inc();
    }
}
