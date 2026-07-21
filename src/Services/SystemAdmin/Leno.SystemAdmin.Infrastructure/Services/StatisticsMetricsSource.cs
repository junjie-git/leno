using Leno.SystemAdmin.Domain.Services;
using Leno.SystemAdmin.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Leno.SystemAdmin.Infrastructure.Services;

/// <summary>
/// 运营数据统计数据源基础设施实现。
/// 通过各 BC 的只读查询接口（gRPC/HTTP）聚合真实指标数据。
/// 当某数据源不可用时返回零值指标并记录告警，不抛异常以保证看板可用性。
/// </summary>
public sealed class StatisticsMetricsSource : IStatisticsDataSource
{
    private readonly StatisticsMetricsQueryClient _queryClient;
    private readonly ILogger<StatisticsMetricsSource> _logger;

    public StatisticsMetricsSource(
        StatisticsMetricsQueryClient queryClient,
        ILogger<StatisticsMetricsSource> logger)
    {
        ArgumentNullException.ThrowIfNull(queryClient);
        ArgumentNullException.ThrowIfNull(logger);
        _queryClient = queryClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<MetricItem>> GetMetricsAsync(
        ReportType reportType,
        ReportPeriod period,
        CancellationToken ct = default)
    {
        try
        {
            return reportType switch
            {
                ReportType.OrderGmv => await _queryClient.QueryOrderGmvAsync(period, ct),
                ReportType.PaymentSuccessRate => await _queryClient.QueryPaymentSuccessRateAsync(period, ct),
                ReportType.PointsIssued => await _queryClient.QueryPointsIssuedAsync(period, ct),
                ReportType.NotificationDelivery => await _queryClient.QueryNotificationDeliveryAsync(period, ct),
                ReportType.AfterSalesVolume => await _queryClient.QueryAfterSalesVolumeAsync(period, ct),
                ReportType.ShopRanking => await _queryClient.QueryShopRankingAsync(period, ct),
                ReportType.ConversionRate => await _queryClient.QueryConversionRateAsync(period, ct),
                _ => throw new ArgumentOutOfRangeException(nameof(reportType), reportType, "未知的报表类型")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "查询运营数据指标失败 ReportType={ReportType}，返回零值占位",
                reportType);
            return CreateFallbackMetrics(reportType);
        }
    }

    private static List<MetricItem> CreateFallbackMetrics(ReportType reportType)
    {
        return reportType switch
        {
            ReportType.OrderGmv => new List<MetricItem>
            {
                new("total_orders", 0m, "单"),
                new("total_gmv", 0m, "CNY"),
                new("avg_order_value", 0m, "CNY"),
                new("order_growth_rate", 0m, "%")
            },
            ReportType.PaymentSuccessRate => new List<MetricItem>
            {
                new("total_payment_attempts", 0m, "次"),
                new("successful_payments", 0m, "次"),
                new("success_rate", 0m, "%"),
                new("failed_payments", 0m, "次")
            },
            ReportType.PointsIssued => new List<MetricItem>
            {
                new("total_points_issued", 0m, "积分"),
                new("total_points_redeemed", 0m, "积分"),
                new("active_users", 0m, "人"),
                new("redeem_rate", 0m, "%")
            },
            ReportType.NotificationDelivery => new List<MetricItem>
            {
                new("total_sent", 0m, "条"),
                new("total_delivered", 0m, "条"),
                new("delivery_rate", 0m, "%"),
                new("total_opened", 0m, "条"),
                new("open_rate", 0m, "%")
            },
            ReportType.AfterSalesVolume => new List<MetricItem>
            {
                new("total_after_sales", 0m, "单"),
                new("total_refund_amount", 0m, "CNY"),
                new("avg_refund_amount", 0m, "CNY"),
                new("return_rate", 0m, "%"),
                new("refund_success_rate", 0m, "%")
            },
            ReportType.ShopRanking => new List<MetricItem>
            {
                new("shop_1_sales", 0m, "CNY"),
                new("shop_1_name", 0, "暂无数据")
            },
            ReportType.ConversionRate => new List<MetricItem>
            {
                new("total_visitors", 0m, "人"),
                new("total_orders", 0m, "单"),
                new("conversion_rate", 0m, "%"),
                new("add_to_cart_rate", 0m, "%"),
                new("cart_to_order_rate", 0m, "%")
            },
            _ => new List<MetricItem> { new("unknown", 0m, "N/A") }
        };
    }
}
