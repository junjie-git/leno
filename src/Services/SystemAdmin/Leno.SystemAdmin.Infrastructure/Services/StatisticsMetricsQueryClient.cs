using Leno.SystemAdmin.Domain.ValueObjects;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;

namespace Leno.SystemAdmin.Infrastructure.Services;

/// <summary>
/// 各 BC 运营数据只读查询客户端，通过 HTTP 调用各 BC 的内部查询端点聚合指标。
/// 每个 BC 暴露 /internal/statistics 端点返回指定时间周期内的聚合数据。
/// 配置节：Statistics:Endpoints，包含 Order/Payment/Points/Notification/AfterSales/Shop/Product 等子键。
/// </summary>
public sealed class StatisticsMetricsQueryClient
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<StatisticsMetricsQueryClient> _logger;

    private const string EndpointsConfigKey = "Statistics:Endpoints";

    public StatisticsMetricsQueryClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<StatisticsMetricsQueryClient> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        _httpClient.Timeout = TimeSpan.FromSeconds(15);
    }

    public async Task<List<MetricItem>> QueryOrderGmvAsync(ReportPeriod period, CancellationToken ct)
    {
        var endpoint = GetEndpoint("Order");
        var url = $"{endpoint}/internal/statistics/order-gmv?start={period.Start:O}&end={period.End:O}";
        return await QueryMetricsAsync(url, ct);
    }

    public async Task<List<MetricItem>> QueryPaymentSuccessRateAsync(ReportPeriod period, CancellationToken ct)
    {
        var endpoint = GetEndpoint("Payment");
        var url = $"{endpoint}/internal/statistics/payment-success-rate?start={period.Start:O}&end={period.End:O}";
        return await QueryMetricsAsync(url, ct);
    }

    public async Task<List<MetricItem>> QueryPointsIssuedAsync(ReportPeriod period, CancellationToken ct)
    {
        var endpoint = GetEndpoint("Points");
        var url = $"{endpoint}/internal/statistics/points-issued?start={period.Start:O}&end={period.End:O}";
        return await QueryMetricsAsync(url, ct);
    }

    public async Task<List<MetricItem>> QueryNotificationDeliveryAsync(ReportPeriod period, CancellationToken ct)
    {
        var endpoint = GetEndpoint("Notification");
        var url = $"{endpoint}/internal/statistics/notification-delivery?start={period.Start:O}&end={period.End:O}";
        return await QueryMetricsAsync(url, ct);
    }

    public async Task<List<MetricItem>> QueryAfterSalesVolumeAsync(ReportPeriod period, CancellationToken ct)
    {
        var endpoint = GetEndpoint("AfterSales");
        var url = $"{endpoint}/internal/statistics/after-sales-volume?start={period.Start:O}&end={period.End:O}";
        return await QueryMetricsAsync(url, ct);
    }

    public async Task<List<MetricItem>> QueryShopRankingAsync(ReportPeriod period, CancellationToken ct)
    {
        var endpoint = GetEndpoint("Shop");
        var url = $"{endpoint}/internal/statistics/shop-ranking?start={period.Start:O}&end={period.End:O}";
        return await QueryMetricsAsync(url, ct);
    }

    public async Task<List<MetricItem>> QueryConversionRateAsync(ReportPeriod period, CancellationToken ct)
    {
        var endpoint = GetEndpoint("Product");
        var url = $"{endpoint}/internal/statistics/conversion-rate?start={period.Start:O}&end={period.End:O}";
        return await QueryMetricsAsync(url, ct);
    }

    private async Task<List<MetricItem>> QueryMetricsAsync(string url, CancellationToken ct)
    {
        var response = await _httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        var metrics = JsonSerializer.Deserialize<List<MetricItemDto>>(json, JsonOptions);

        if (metrics is null || metrics.Count == 0)
        {
            throw new InvalidOperationException($"数据源返回空指标列表 URL={url}");
        }

        return metrics.Select(m => new MetricItem(m.Key, m.Value, m.Unit)).ToList();
    }

    private string GetEndpoint(string bcName)
    {
        var endpoint = _configuration[$"{EndpointsConfigKey}:{bcName}"];
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new InvalidOperationException(
                $"未配置 BC={bcName} 的统计查询端点，配置键：{EndpointsConfigKey}:{bcName}");
        }
        return endpoint.TrimEnd('/');
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private sealed class MetricItemDto
    {
        public string Key { get; set; } = string.Empty;
        public decimal Value { get; set; }
        public string Unit { get; set; } = string.Empty;
    }
}
