using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Services;
using Leno.SystemAdmin.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Leno.SystemAdmin.Infrastructure.Services;

/// <summary>
/// 运营数据统计聚合服务实现，按报表类型从各数据源聚合指标数据。
/// 当前使用简化的内存计算生成模拟数据，后续可替换为 ES 查询或事件溯源聚合。
/// </summary>
public sealed class StatisticsAggregationService : IStatisticsAggregationService
{
    private readonly ILogger<StatisticsAggregationService> _logger;

    public StatisticsAggregationService(ILogger<StatisticsAggregationService> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<DashboardReport> AggregateAsync(
        ReportType reportType,
        ReportPeriod period,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "开始聚合运营数据 ReportType={ReportType} Start={Start} End={End}",
            reportType, period.Start, period.End);

        var metrics = reportType switch
        {
            ReportType.OrderGmv => AggregateOrderGmv(period),
            ReportType.PaymentSuccessRate => AggregatePaymentSuccessRate(period),
            ReportType.PointsIssued => AggregatePointsIssued(period),
            ReportType.NotificationDelivery => AggregateNotificationDelivery(period),
            ReportType.AfterSalesVolume => AggregateAfterSalesVolume(period),
            ReportType.ShopRanking => AggregateShopRanking(period),
            ReportType.ConversionRate => AggregateConversionRate(period),
            _ => throw new ArgumentOutOfRangeException(nameof(reportType), reportType, "未知的报表类型")
        };

        var granularity = DetermineGranularity(period);

        var report = DashboardReport.Create(
            Guid.NewGuid(),
            reportType,
            period,
            metrics,
            granularity);

        _logger.LogInformation(
            "运营数据聚合完成 ReportType={ReportType} ReportId={ReportId} MetricCount={MetricCount}",
            reportType, report.ReportId, metrics.Count);

        return Task.FromResult(report);
    }

    private static List<MetricItem> AggregateOrderGmv(ReportPeriod period)
    {
        var days = (period.End - period.Start).Days;
        if (days <= 0) days = 1;

        var totalOrders = (decimal)(new Random().Next(1000, 5000) * days);
        var totalGmv = (decimal)(new Random().Next(50000, 200000) * days);
        var avgOrderValue = totalOrders > 0 ? totalGmv / totalOrders : 0;

        return new List<MetricItem>
        {
            new("total_orders", totalOrders, "单"),
            new("total_gmv", totalGmv, "CNY"),
            new("avg_order_value", Math.Round(avgOrderValue, 2), "CNY"),
            new("order_growth_rate", Math.Round((decimal)(new Random().NextDouble() * 40 - 10), 2), "%")
        };
    }

    private static List<MetricItem> AggregatePaymentSuccessRate(ReportPeriod period)
    {
        var totalAttempts = (decimal)new Random().Next(5000, 20000);
        var successful = (decimal)new Random().Next(4500, 19500);
        var successRate = totalAttempts > 0 ? Math.Round(successful / totalAttempts * 100, 2) : 0;

        return new List<MetricItem>
        {
            new("total_payment_attempts", totalAttempts, "次"),
            new("successful_payments", successful, "次"),
            new("success_rate", successRate, "%"),
            new("failed_payments", totalAttempts - successful, "次"),
            new("wechat_success_rate", Math.Round((decimal)(new Random().NextDouble() * 20 + 75), 2), "%"),
            new("alipay_success_rate", Math.Round((decimal)(new Random().NextDouble() * 20 + 75), 2), "%")
        };
    }

    private static List<MetricItem> AggregatePointsIssued(ReportPeriod period)
    {
        var days = (period.End - period.Start).Days;
        if (days <= 0) days = 1;

        var totalIssued = (decimal)(new Random().Next(10000, 50000) * days);
        var totalRedeemed = (decimal)(new Random().Next(5000, 30000) * days);
        var activeUsers = (decimal)(new Random().Next(500, 3000) * days);

        return new List<MetricItem>
        {
            new("total_points_issued", totalIssued, "积分"),
            new("total_points_redeemed", totalRedeemed, "积分"),
            new("active_users", activeUsers, "人"),
            new("avg_points_per_user", activeUsers > 0 ? Math.Round(totalIssued / activeUsers, 2) : 0, "积分"),
            new("redeem_rate", totalIssued > 0 ? Math.Round(totalRedeemed / totalIssued * 100, 2) : 0, "%")
        };
    }

    private static List<MetricItem> AggregateNotificationDelivery(ReportPeriod period)
    {
        var totalSent = (decimal)new Random().Next(10000, 100000);
        var totalDelivered = (decimal)new Random().Next((int)(totalSent * 0.85m), (int)totalSent);
        var totalOpened = (decimal)new Random().Next((int)(totalDelivered * 0.4m), (int)(totalDelivered * 0.7m));

        return new List<MetricItem>
        {
            new("total_sent", totalSent, "条"),
            new("total_delivered", totalDelivered, "条"),
            new("delivery_rate", totalSent > 0 ? Math.Round(totalDelivered / totalSent * 100, 2) : 0, "%"),
            new("total_opened", totalOpened, "条"),
            new("open_rate", totalDelivered > 0 ? Math.Round(totalOpened / totalDelivered * 100, 2) : 0, "%"),
            new("sms_delivery_rate", Math.Round((decimal)(new Random().NextDouble() * 10 + 88), 2), "%"),
            new("push_delivery_rate", Math.Round((decimal)(new Random().NextDouble() * 15 + 70), 2), "%")
        };
    }

    private static List<MetricItem> AggregateAfterSalesVolume(ReportPeriod period)
    {
        var days = (period.End - period.Start).Days;
        if (days <= 0) days = 1;

        var totalAfterSales = (decimal)(new Random().Next(50, 300) * days);
        var totalRefundAmount = (decimal)(new Random().Next(5000, 50000) * days);
        var avgRefundAmount = totalAfterSales > 0 ? totalRefundAmount / totalAfterSales : 0;

        return new List<MetricItem>
        {
            new("total_after_sales", totalAfterSales, "单"),
            new("total_refund_amount", totalRefundAmount, "CNY"),
            new("avg_refund_amount", Math.Round(avgRefundAmount, 2), "CNY"),
            new("return_rate", Math.Round((decimal)(new Random().NextDouble() * 5), 2), "%"),
            new("refund_success_rate", Math.Round((decimal)(new Random().NextDouble() * 10 + 85), 2), "%")
        };
    }

    private static List<MetricItem> AggregateShopRanking(ReportPeriod period)
    {
        var shopNames = new[] { "官方旗舰店", "品质生活馆", "数码潮品店", "美食天地", "时尚服饰店",
            "家居好物", "运动户外", "母婴优选", "美妆护肤", "图书文创" };

        var metrics = new List<MetricItem>();
        for (var i = 0; i < 10; i++)
        {
            var sales = (decimal)new Random().Next(10000 * (10 - i), 100000 * (10 - i));
            metrics.Add(new MetricItem($"shop_{i + 1}_sales", sales, "CNY"));
            metrics.Add(new MetricItem($"shop_{i + 1}_name", i, shopNames[i]));
            metrics.Add(new MetricItem($"shop_{i + 1}_orders", (decimal)new Random().Next(100 * (10 - i), 1000 * (10 - i)), "单"));
        }

        return metrics;
    }

    private static List<MetricItem> AggregateConversionRate(ReportPeriod period)
    {
        var totalVisitors = (decimal)new Random().Next(50000, 500000);
        var totalOrders = (decimal)new Random().Next(1000, 15000);
        var conversionRate = totalVisitors > 0 ? Math.Round(totalOrders / totalVisitors * 100, 2) : 0;
        var addToCartVisitors = (decimal)new Random().Next((int)(totalVisitors * 0.08m), (int)(totalVisitors * 0.2m));
        var cartToOrderRate = addToCartVisitors > 0 ? Math.Round(totalOrders / addToCartVisitors * 100, 2) : 0;

        return new List<MetricItem>
        {
            new("total_visitors", totalVisitors, "人"),
            new("total_orders", totalOrders, "单"),
            new("conversion_rate", conversionRate, "%"),
            new("add_to_cart_rate", totalVisitors > 0 ? Math.Round(addToCartVisitors / totalVisitors * 100, 2) : 0, "%"),
            new("cart_to_order_rate", cartToOrderRate, "%"),
            new("avg_session_duration", Math.Round((decimal)(new Random().NextDouble() * 5 + 2), 2), "分钟"),
            new("bounce_rate", Math.Round((decimal)(new Random().NextDouble() * 20 + 30), 2), "%")
        };
    }

    private static string DetermineGranularity(ReportPeriod period)
    {
        var span = period.End - period.Start;
        if (span.TotalHours <= 24) return "hourly";
        if (span.TotalDays <= 7) return "daily";
        return "weekly";
    }
}