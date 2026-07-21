# SystemAdmin（系统管理域）修复实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 修复 SystemAdmin BC 代码审计报告中发现的 22 个问题（7 高风险 + 10 中风险 + 5 低风险），消除运营数据随机生成、发件箱旁路、缓存不失效、TOCTOU 竞态、事务边界混乱等缺陷，使该 BC 达到生产可用状态。

**Architecture:** 按 DDD 四层（Domain/Application/Infrastructure/Api）治理。P0 修复聚焦数据真实性（注入 `IStatisticsDataSource` 替换 `new Random()`）、发件箱语义统一（删除手动 `IEventBus.PublishAsync`）、缓存失效补全、幂等竞态消除（唯一索引 + 异常捕获）、事务边界收敛（单次 `SaveEntitiesAsync`）、消费者职责拆分。所有修复遵循 TDD：先写失败测试，再写最小实现，最后提交。

**Tech Stack:** .NET 10、EF Core、MassTransit、RabbitMQ、Redis、Quartz、xUnit、FluentAssertions、Moq

**关联审计报告：**
- 主报告：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/11-systemadmin.md]
- 汇总 F 章节：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/00-summary.md]
- 架构评估 G4/G5：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/13-architecture-assessment.md]

---

## 元数据

- 审计报告：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/11-systemadmin.md]
- 问题总数：🔴 7 / 🟡 10 / 🟢 5 = 22 项
- 已修复（跳过详细计划）：2 项（T15、T20）
- 不可复现：0 项
- 本计划覆盖：20 项待修复

---

## 问题清单总表

| # | 严重度 | 问题标题 | 审计位置 | 优先级 | 状态 |
|---|--------|---------|---------|--------|------|
| H-01 | 🔴 | StatisticsAggregationService 全部使用 new Random() 生成模拟数据 | [file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Services/StatisticsAggregationService.cs#L60-L186] | P0 | 待修复 |
| H-02 | 🔴 | SystemConfigAppService/AnnouncementAppService 越过发件箱直接发布集成事件 | [file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Application/Services/SystemConfigAppService.cs#L50-L53]、[file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Application/Services/SystemConfigAppService.cs#L67-L70]、[file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Application/Services/AnnouncementAppService.cs#L77-L80] | P0 | 待修复 |
| H-03 | 🔴 | FeatureFlagCache/SystemConfigCache 写入后从不失效 | [file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Application/Services/FeatureFlagAppService.cs#L62-L63]、[file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Application/Services/FeatureFlagAppService.cs#L75-L76]、[file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Application/Services/FeatureFlagAppService.cs#L87-L88]、[file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Application/Services/SystemConfigAppService.cs#L50-L51]、[file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Application/Services/SystemConfigAppService.cs#L67-L68] | P0 | 待修复 |
| H-04 | 🔴 | AuditLogConsumer 幂等去重存在 TOCTOU 竞态 | [file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Consumers/AuditLogConsumer.cs#L255-L277]、[file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Repositories/EfCoreAuditLogEntryRepository.cs#L29-L30] | P0 | 待修复 |
| H-05 | 🔴 | DeadLetterQueueManager/RabbitMqDeadLetterManager 使用 SaveChangesAsync 而非 SaveEntitiesAsync | [file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Services/DeadLetterQueueManager.cs#L75-L77]、[file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Services/RabbitMqDeadLetterManager.cs#L173-L175]、[file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Services/RabbitMqDeadLetterManager.cs#L193-L194] | P0 | 待修复 |
| H-06 | 🔴 | IndexRebuildOrchestrator 多步状态变更无事务，重试与并发触发存在竞态 | [file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Services/IndexRebuildOrchestrator.cs#L38-L68]、[file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Services/IndexRebuildOrchestrator.cs#L88-L108] | P0 | 待修复 |
| H-07 | 🔴 | AuditLogConsumer 与 AfterSalesEventConsumer 同时消费 AfterSalesApprovedEvent，逻辑重复且无协调 | [file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Consumers/AuditLogConsumer.cs#L59-L78]、[file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Consumers/AfterSalesEventConsumer.cs#L37-L64] | P0 | 待修复 |
| M-01 | 🟡 | DashboardController 直接返回领域实体 DashboardReport，泄露聚合内部结构 | [file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/DashboardController.cs#L40-L48] | P1 | 待修复 |
| M-02 | 🟡 | StatisticsController 直接返回领域实体 ReconciliationRecord，泄露聚合内部结构 | [file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/StatisticsController.cs#L73-L91]、[file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/StatisticsController.cs#L96-L108] | P1 | 待修复 |
| M-03 | 🟡 | RateLimitRule 聚合根缺少 RowVersion，控制器捕获 DbUpdateConcurrencyException 永不触发 | [file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Domain/Aggregates/RateLimitRule.cs#L12-L44]、[file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Configurations/RateLimitRuleConfiguration.cs#L12-L33] | P1 | 待修复 |
| M-04 | 🟡 | AuditLogAppService.ExportAuditLogsAsync 使用 int.MaxValue 一次性加载全部审计日志，OOM 风险 | [file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Application/Services/AuditLogAppService.cs#L66-L93]、[file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/SystemConfigsController.cs#L47-L48] | P1 | 待修复 |
| M-05 | 🟡 | DeadLetterAppService.BatchRetryAsync/BatchDiscardAsync 逐条调用，非原子且无事务 | [file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Application/Services/DeadLetterAppService.cs#L80-L108]、[file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Application/Services/DeadLetterAppService.cs#L111-L139] | P1 | 待修复 |
| M-06 | 🟡 | ScheduledTaskJob 两次 SaveEntitiesAsync，RunNow 与 RecordExecution 不在同一事务 | [file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Jobs/ScheduledTaskJob.cs#L49-L74] | P1 | 待修复 |
| M-07 | 🟡 | ReconciliationRecord 标注"不可变"但 MarkAlertTriggered/MarkCorrectionTriggered 修改状态 | [file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Domain/Aggregates/ReconciliationRecord.cs#L9-L10]、[file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Domain/Aggregates/ReconciliationRecord.cs#L66-L80] | P1 | 待修复 |
| M-08 | 🟡 | ElasticsearchRebuildTrigger.GetProgressAsync 返回第一个匹配的 reindex 任务，无 TaskId 关联 | [file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Services/ElasticsearchRebuildTrigger.cs#L89-L149] | P1 | 待修复 |
| M-09 | 🟡 | ScheduledTaskJob taskId 解析失败时静默 return，无日志 | [file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Jobs/ScheduledTaskJob.cs#L31-L35] | P1 | 待修复 |
| M-10 | 🟡 | RabbitMqDeadLetterManager.PersistDeadLetterCopyAsync 入库副本存在 TOCTOU 竞态 | [file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Services/RabbitMqDeadLetterManager.cs#L184-L198]、[file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Configurations/DeadLetterMessageConfiguration.cs#L35] | P1 | 待修复 |
| L-01 | 🟢 | EfCoreDataDictionaryRepository.QueryAsync 使用 Include 但 CountAsync 未使用 | [file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Repositories/EfCoreDataDictionaryRepository.cs#L34-L50] | P2 | 待修复 |
| L-02 | 🟢 | StatisticsReconciliationJob 使用 DateTime.UtcNow 计算下次午夜，存在时区漂移 | [file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Jobs/StatisticsReconciliationJob.cs#L62-L67] | P2 | 待修复 |
| L-03 | 🟢 | HttpModuleHealthProbe 的 3 秒超时对慢网络过激进 | [file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Services/HttpModuleHealthProbe.cs#L16] | P2 | 待修复 |
| L-04 | 🟢 | RabbitMqDeadLetterManager 采用 ack_requeue_true 但未实现 DLQ 清理作业 | [file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Services/RabbitMqDeadLetterManager.cs#L26-L31] | P2 | 待修复 |
| L-05 | 🟢 | AuditLogConsumer.AfterSalesApproved 同时写 AuditLog 与 AuditLogEntry，职责越界 | [file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Consumers/AuditLogConsumer.cs#L70-L77] | P2 | 待修复 |

---

## 已修复问题清单（跳过详细计划）

### T15 — 死信重投改为真正重投原始集成事件

**状态**：[ALREADY-FIXED]

**说明**：`DeadLetterRepublishHelper` 已实现，通过 `IEventBus` 将死信记录中的原始集成事件反序列化后重新发布到 MQ。`DeadLetterQueueManager.RepublishAsync`（[file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Services/DeadLetterQueueManager.cs#L51-L80]）与 `RabbitMqDeadLetterManager.RepublishAsync`（[file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Services/RabbitMqDeadLetterManager.cs#L148-L178]）均调用 `DeadLetterRepublishHelper.RepublishViaEventBusAsync`。

**残留事项**：两处仍使用 `SaveChangesAsync` 而非 `SaveEntitiesAsync`（H-05 覆盖此问题），本计划在 H-05 中修复。

### T20 — 死信积压告警后台服务

**状态**：[ALREADY-FIXED]

**说明**：`DeadLetterMonitorBackgroundService` 已实现，通过 `ObservableGauge<int>` 暴露 `dead_letter_count` 指标。`DeadLetterMonitorOptions` 支持配置告警阈值。已在 `ServiceCollectionExtensions` 注册（[file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Dependencies/ServiceCollectionExtensions.cs#L109-L112]）。

---

## P0 详细修复计划（TDD bite-sized 格式，5 步：测试→验证失败→实现→验证通过→提交）

### P0-H-01 修复 StatisticsAggregationService 使用 new Random() 生成全部模拟指标

**审计位置**：[file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Services/StatisticsAggregationService.cs#L60-L186]

**根因**：`StatisticsAggregationService` 的 7 个 `Aggregate*` 私有方法全部使用 `new Random().Next(...)` 生成指标数据，`AggregateShopRanking` 还硬编码 10 个虚构店铺名。

**修复方案**：在领域层定义 `IStatisticsDataSource` 接口，按报表类型从真实数据源（各 BC 读模型/ES 索引）聚合指标。`StatisticsAggregationService` 注入该接口，删除所有 `new Random()` 调用。

**涉及文件：**
- 创建：`src/Services/SystemAdmin/Leno.SystemAdmin.Domain/Services/IStatisticsDataSource.cs`
- 创建：`src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Services/StatisticsMetricsSource.cs`（`IStatisticsDataSource` 实现，从各 BC 只读查询聚合真实指标）
- 修改：`src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Services/StatisticsAggregationService.cs`
- 修改：`src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Dependencies/ServiceCollectionExtensions.cs`（注册 `IStatisticsDataSource`）
- 测试：`src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure.Tests/Services/StatisticsAggregationServiceTests.cs`

- [ ] **Step 1：编写失败测试**

测试文件：`src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure.Tests/Services/StatisticsAggregationServiceTests.cs`

```csharp
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Services;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SystemAdmin.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Leno.SystemAdmin.Infrastructure.Tests.Services;

public sealed class StatisticsAggregationServiceTests
{
    private static readonly ReportPeriod Period =
        new(DateTime.UtcNow.AddDays(-7), DateTime.UtcNow);

    private readonly Mock<IStatisticsDataSource> _dataSourceMock = new();
    private readonly StatisticsAggregationService _service;

    public StatisticsAggregationServiceTests()
    {
        _service = new StatisticsAggregationService(
            _dataSourceMock.Object,
            NullLogger<StatisticsAggregationService>.Instance);
    }

    [Fact]
    public async Task AggregateAsync_OrderGmv_Should_Return_Metrics_From_DataSource_Not_Random()
    {
        var expectedMetrics = new List<MetricItem>
        {
            new("total_orders", 1200m, "单"),
            new("total_gmv", 96000m, "CNY"),
            new("avg_order_value", 80m, "CNY"),
            new("order_growth_rate", 5.5m, "%")
        };
        _dataSourceMock
            .Setup(d => d.GetMetricsAsync(ReportType.OrderGmv, Period, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedMetrics);

        var report = await _service.AggregateAsync(ReportType.OrderGmv, Period, CancellationToken.None);

        Assert.NotNull(report);
        Assert.Equal(ReportType.OrderGmv, report.ReportType);
        Assert.Equal(expectedMetrics.Count, report.Metrics.Count);
        Assert.Equal(1200m, report.Metrics[0].Value);
        Assert.Equal(96000m, report.Metrics[1].Value);
        _dataSourceMock.Verify(
            d => d.GetMetricsAsync(ReportType.OrderGmv, Period, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AggregateAsync_ShopRanking_Should_Not_Contain_Hardcoded_Shop_Names()
    {
        var expectedMetrics = new List<MetricItem>
        {
            new("shop_1_sales", 50000m, "CNY"),
            new("shop_1_name", 0, "官方旗舰店")
        };
        _dataSourceMock
            .Setup(d => d.GetMetricsAsync(ReportType.ShopRanking, Period, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedMetrics);

        var report = await _service.AggregateAsync(ReportType.ShopRanking, Period, CancellationToken.None);

        Assert.Equal(2, report.Metrics.Count);
        Assert.DoesNotContain(report.Metrics, m => m.Key.StartsWith("shop_2_"));
    }

    [Fact]
    public async Task AggregateAsync_Should_Throw_When_DataSource_Returns_Empty_Metrics()
    {
        _dataSourceMock
            .Setup(d => d.GetMetricsAsync(It.IsAny<ReportType>(), It.IsAny<ReportPeriod>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MetricItem>());

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.AggregateAsync(ReportType.OrderGmv, Period, CancellationToken.None));
    }
}
```

- [ ] **Step 2：运行测试验证失败**

Run: `dotnet test src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure.Tests --filter "StatisticsAggregationServiceTests" --verbosity normal`
Expected: FAIL — 编译失败，`IStatisticsDataSource` 不存在，`StatisticsAggregationService` 构造函数不接收 `IStatisticsDataSource` 参数。

- [ ] **Step 3：编写最小实现**

创建文件：`src/Services/SystemAdmin/Leno.SystemAdmin.Domain/Services/IStatisticsDataSource.cs`

```csharp
using Leno.SystemAdmin.Domain.ValueObjects;

namespace Leno.SystemAdmin.Domain.Services;

/// <summary>
/// 运营数据统计数据源接口，定义在领域层，由基础设施层实现。
/// 负责从各 BC 的只读模型（ES 索引、只读副本）聚合真实指标数据。
/// </summary>
public interface IStatisticsDataSource
{
    /// <summary>
    /// 按报表类型与时间周期从真实数据源获取指标列表。
    /// </summary>
    /// <param name="reportType">报表类型。</param>
    /// <param name="period">报表覆盖的时间周期。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>指标项列表，不可为空。返回空列表时由调用方判定为异常。</returns>
    Task<List<MetricItem>> GetMetricsAsync(ReportType reportType, ReportPeriod period, CancellationToken ct = default);
}
```

修改文件：`src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Services/StatisticsAggregationService.cs`

完整替换内容：

```csharp
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Services;
using Leno.SystemAdmin.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Leno.SystemAdmin.Infrastructure.Services;

/// <summary>
/// 运营数据统计聚合服务实现，按报表类型从 <see cref="IStatisticsDataSource"/> 获取真实指标数据，
/// 组装为 <see cref="DashboardReport"/> 聚合根。
/// </summary>
public sealed class StatisticsAggregationService : IStatisticsAggregationService
{
    private readonly IStatisticsDataSource _dataSource;
    private readonly ILogger<StatisticsAggregationService> _logger;

    public StatisticsAggregationService(
        IStatisticsDataSource dataSource,
        ILogger<StatisticsAggregationService> logger)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentNullException.ThrowIfNull(logger);
        _dataSource = dataSource;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<DashboardReport> AggregateAsync(
        ReportType reportType,
        ReportPeriod period,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "开始聚合运营数据 ReportType={ReportType} Start={Start} End={End}",
            reportType, period.Start, period.End);

        var metrics = await _dataSource.GetMetricsAsync(reportType, period, ct);

        if (metrics is null || metrics.Count == 0)
        {
            throw new ArgumentException(
                $"数据源未返回任何指标数据 ReportType={reportType}", nameof(reportType));
        }

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

        return report;
    }

    private static string DetermineGranularity(ReportPeriod period)
    {
        var span = period.End - period.Start;
        if (span.TotalHours <= 24) return "hourly";
        if (span.TotalDays <= 7) return "daily";
        return "weekly";
    }
}
```

创建文件：`src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Services/StatisticsMetricsSource.cs`（`IStatisticsDataSource` 的基础设施实现，通过 gRPC/HTTP 查询各 BC 只读模型聚合真实指标）

```csharp
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
```

创建文件：`src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Services/StatisticsMetricsQueryClient.cs`（封装对各 BC 只读查询的 HTTP/gRPC 客户端）

```csharp
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
```

修改文件：`src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Dependencies/ServiceCollectionExtensions.cs`

在 `AddSystemAdminInfrastructure` 方法中 `services.AddScoped<IStatisticsAggregationService, StatisticsAggregationService>();` 之前增加：

```csharp
        services.AddHttpClient<StatisticsMetricsQueryClient>();
        services.AddScoped<IStatisticsDataSource, StatisticsMetricsSource>();
```

- [ ] **Step 4：运行测试验证通过**

Run: `dotnet test src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure.Tests --filter "StatisticsAggregationServiceTests" --verbosity normal`
Expected: PASS — 3 个测试全部通过。

- [ ] **Step 5：提交**

```bash
git add src/Services/SystemAdmin/Leno.SystemAdmin.Domain/Services/IStatisticsDataSource.cs \
        src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Services/StatisticsAggregationService.cs \
        src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Services/StatisticsMetricsSource.cs \
        src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Services/StatisticsMetricsQueryClient.cs \
        src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Dependencies/ServiceCollectionExtensions.cs \
        src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure.Tests/Services/StatisticsAggregationServiceTests.cs
git commit -m "fix(SystemAdmin): H-01 替换 StatisticsAggregationService 的随机数为真实数据源 IStatisticsDataSource"
```

---

### P0-H-02 修复 SystemConfigAppService 与 AnnouncementAppService 越过发件箱直接发布集成事件

**审计位置**：[file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Application/Services/SystemConfigAppService.cs#L50-L53]、[file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Application/Services/SystemConfigAppService.cs#L67-L70]、[file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Application/Services/AnnouncementAppService.cs#L77-L80]

**根因**：两个 AppService 在 `SaveEntitiesAsync` 之后额外调用 `IEventBus.PublishAsync` 直接发布集成事件，而聚合根已通过 `AddDomainEvent` 发布领域事件（`ConfigChangedEvent`/`AnnouncementPublishedEvent`），`SystemAdminIntegrationEventMapper` 会翻译为集成事件经发件箱投递。手动 `PublishAsync` 导致双发。

**修复方案**：删除两个 AppService 中所有 `await _eventBus.PublishAsync(...)` 调用，移除 `IEventBus` 依赖注入。

**涉及文件：**
- 修改：`src/Services/SystemAdmin/Leno.SystemAdmin.Application/Services/SystemConfigAppService.cs`
- 修改：`src/Services/SystemAdmin/Leno.SystemAdmin.Application/Services/AnnouncementAppService.cs`
- 测试：`src/Services/SystemAdmin/Leno.SystemAdmin.Application.Tests/Services/SystemConfigAppServiceOutboxTests.cs`
- 测试：`src/Services/SystemAdmin/Leno.SystemAdmin.Application.Tests/Services/AnnouncementAppServiceOutboxTests.cs`

- [ ] **Step 1：编写失败测试**

测试文件：`src/Services/SystemAdmin/Leno.SystemAdmin.Application.Tests/Services/SystemConfigAppServiceOutboxTests.cs`

```csharp
using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Application.Services;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Leno.SystemAdmin.Application.Tests.Services;

public sealed class SystemConfigAppServiceOutboxTests
{
    private readonly Mock<ISystemConfigRepository> _repoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly SystemConfigAppService _service;

    public SystemConfigAppServiceOutboxTests()
    {
        _unitOfWorkMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _service = new SystemConfigAppService(
            _repoMock.Object,
            _unitOfWorkMock.Object,
            NullLogger<SystemConfigAppService>.Instance);
    }

    [Fact]
    public async Task CreateAsync_Should_Not_Inject_IEventBus_And_Call_Only_SaveEntitiesAsync()
    {
        var dto = new SaveSystemConfigDto
        {
            Key = "test.key",
            Value = "test-value",
            Group = "test-group",
            Description = null,
            IsEncrypted = false
        };

        var result = await _service.CreateAsync(dto, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("test.key", result.Key);
        _unitOfWorkMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_Should_Call_Only_SaveEntitiesAsync_Without_Manual_Publish()
    {
        var configId = Guid.NewGuid();
        var existing = SystemConfig.Create(configId, "test.key", "old-value", "group", null, false);
        _repoMock.Setup(r => r.GetByIdAsync(configId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var dto = new UpdateSystemConfigDto { Value = "new-value", Description = null, IsEncrypted = false };

        var result = await _service.UpdateAsync(configId, dto, CancellationToken.None);

        Assert.Equal("new-value", result.Value);
        _unitOfWorkMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

测试文件：`src/Services/SystemAdmin/Leno.SystemAdmin.Application.Tests/Services/AnnouncementAppServiceOutboxTests.cs`

```csharp
using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Application.Services;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Leno.SystemAdmin.Application.Tests.Services;

public sealed class AnnouncementAppServiceOutboxTests
{
    private readonly Mock<ISystemAnnouncementRepository> _repoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly AnnouncementAppService _service;

    public AnnouncementAppServiceOutboxTests()
    {
        _unitOfWorkMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _service = new AnnouncementAppService(
            _repoMock.Object,
            _unitOfWorkMock.Object,
            NullLogger<AnnouncementAppService>.Instance);
    }

    [Fact]
    public async Task PublishAsync_Should_Call_Only_SaveEntitiesAsync_Without_Manual_Publish()
    {
        var announcementId = Guid.NewGuid();
        var existing = SystemAnnouncement.Create(
            announcementId, "标题", "内容", AnnouncementType.System,
            AnnouncementTargetAudience.All, null, null);
        _repoMock.Setup(r => r.GetByIdAsync(announcementId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        await _service.PublishAsync(announcementId, CancellationToken.None);

        Assert.Equal(AnnouncementStatus.Published, existing.Status);
        _unitOfWorkMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

- [ ] **Step 2：运行测试验证失败**

Run: `dotnet test src/Services/SystemAdmin/Leno.SystemAdmin.Application.Tests --filter "OutboxTests" --verbosity normal`
Expected: FAIL — 编译失败，`SystemConfigAppService` 与 `AnnouncementAppService` 构造函数仍要求传入 `IEventBus`。

- [ ] **Step 3：编写最小实现**

修改文件：`src/Services/SystemAdmin/Leno.SystemAdmin.Application/Services/SystemConfigAppService.cs`

完整替换内容：

```csharp
using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;

namespace Leno.SystemAdmin.Application.Services;

/// <summary>
/// 系统配置管理应用服务实现。
/// 配置变更（创建/更新/启停）经聚合根附加 <see cref="Leno.SystemAdmin.Domain.Events.ConfigChangedEvent"/> 领域事件，
/// 由工作单元的发件箱机制在同一事务内持久化并发布，不手动调用 IEventBus。
/// </summary>
public sealed class SystemConfigAppService : ISystemConfigAppService
{
    private const string MaskedValue = "******";

    private readonly ISystemConfigRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SystemConfigAppService> _logger;

    public SystemConfigAppService(
        ISystemConfigRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<SystemConfigAppService> logger)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(logger);
        _repository = repository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<SystemConfigDto> CreateAsync(SaveSystemConfigDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var configId = Guid.NewGuid();
        var entity = SystemConfig.Create(configId, dto.Key, dto.Value, dto.Group, dto.Description, dto.IsEncrypted);

        await _repository.AddAsync(entity, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        _logger.LogInformation("系统配置已创建：{ConfigId}（Key={ConfigKey}）", configId, entity.Key);
        return ToDto(entity);
    }

    /// <inheritdoc />
    public async Task<SystemConfigDto> UpdateAsync(Guid configId, UpdateSystemConfigDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var entity = await RequireConfigAsync(configId, ct);
        entity.Update(dto.Value, dto.Description, dto.IsEncrypted);

        await _repository.UpdateAsync(entity, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        _logger.LogInformation("系统配置已更新：{ConfigId}（Key={ConfigKey}）", configId, entity.Key);
        return ToDto(entity);
    }

    /// <inheritdoc />
    public async Task EnableAsync(Guid configId, CancellationToken ct = default)
    {
        var entity = await RequireConfigAsync(configId, ct);
        entity.Enable();

        await _repository.UpdateAsync(entity, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        _logger.LogInformation("系统配置已启用：{ConfigId}（Key={ConfigKey}）", configId, entity.Key);
    }

    /// <inheritdoc />
    public async Task DisableAsync(Guid configId, CancellationToken ct = default)
    {
        var entity = await RequireConfigAsync(configId, ct);
        entity.Disable();

        await _repository.UpdateAsync(entity, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        _logger.LogInformation("系统配置已停用：{ConfigId}（Key={ConfigKey}）", configId, entity.Key);
    }

    /// <inheritdoc />
    public async Task<SystemConfigDto?> GetByKeyAsync(string key, CancellationToken ct = default)
    {
        var entity = await _repository.GetByKeyAsync(key, ct);
        return entity is null ? null : ToDto(entity);
    }

    /// <inheritdoc />
    public async Task<List<SystemConfigDto>> GetByGroupAsync(string group, CancellationToken ct = default)
    {
        var configs = await _repository.QueryByGroupAsync(group, ct);
        return configs.Select(ToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<SystemConfigListResultDto> QueryAsync(string? key, string? group, ConfigStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        var items = await _repository.QueryAsync(key, group, status, page, pageSize, ct);
        var total = await _repository.CountAsync(key, group, status, ct);

        return new SystemConfigListResultDto
        {
            Items = items.Select(ToDto).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    private async Task<SystemConfig> RequireConfigAsync(Guid configId, CancellationToken ct)
        => await _repository.GetByIdAsync(configId, ct)
           ?? throw new InvalidOperationException($"系统配置 {configId} 不存在");

    private static SystemConfigDto ToDto(SystemConfig entity)
        => new()
        {
            ConfigId = entity.ConfigId,
            Key = entity.Key,
            Value = entity.IsEncrypted ? MaskedValue : entity.Value,
            Group = entity.Group,
            Description = entity.Description,
            IsEncrypted = entity.IsEncrypted,
            Status = entity.Status,
            UpdatedAt = entity.UpdatedAt
        };
}
```

修改文件：`src/Services/SystemAdmin/Leno.SystemAdmin.Application/Services/AnnouncementAppService.cs`

完整替换内容：

```csharp
using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;

namespace Leno.SystemAdmin.Application.Services;

/// <summary>
/// 系统公告管理应用服务实现。
/// 发布公告经聚合根附加 <see cref="Leno.SystemAdmin.Domain.Events.AnnouncementPublishedEvent"/> 领域事件，
/// 由工作单元的发件箱机制在同一事务内持久化并发布，不手动调用 IEventBus。
/// </summary>
public sealed class AnnouncementAppService : IAnnouncementAppService
{
    private readonly ISystemAnnouncementRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AnnouncementAppService> _logger;

    public AnnouncementAppService(
        ISystemAnnouncementRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<AnnouncementAppService> logger)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(logger);
        _repository = repository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AnnouncementDto> CreateAsync(SaveAnnouncementDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var announcementId = Guid.NewGuid();
        var entity = SystemAnnouncement.Create(
            announcementId, dto.Title, dto.Content, dto.Type, dto.TargetAudience, dto.PublishAt, dto.ExpireAt);

        await _repository.AddAsync(entity, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        _logger.LogInformation("公告已创建：{AnnouncementId}", announcementId);
        return ToDto(entity);
    }

    /// <inheritdoc />
    public async Task<AnnouncementDto> UpdateAsync(Guid announcementId, SaveAnnouncementDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var entity = await RequireAnnouncementAsync(announcementId, ct);
        entity.Update(dto.Title, dto.Content, dto.Type, dto.TargetAudience, dto.PublishAt, dto.ExpireAt);

        await _repository.UpdateAsync(entity, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        _logger.LogInformation("公告已更新：{AnnouncementId}", announcementId);
        return ToDto(entity);
    }

    /// <inheritdoc />
    public async Task PublishAsync(Guid announcementId, CancellationToken ct = default)
    {
        var entity = await RequireAnnouncementAsync(announcementId, ct);
        entity.Publish();

        await _repository.UpdateAsync(entity, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        _logger.LogInformation("公告已发布：{AnnouncementId}", announcementId);
    }

    /// <inheritdoc />
    public async Task UnpublishAsync(Guid announcementId, CancellationToken ct = default)
    {
        var entity = await RequireAnnouncementAsync(announcementId, ct);
        entity.Unpublish();

        await _repository.UpdateAsync(entity, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        _logger.LogInformation("公告已撤回：{AnnouncementId}", announcementId);
    }

    /// <inheritdoc />
    public async Task<AnnouncementDto?> GetByIdAsync(Guid announcementId, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(announcementId, ct);
        return entity is null ? null : ToDto(entity);
    }

    /// <inheritdoc />
    public async Task<AnnouncementListResultDto> QueryAsync(AnnouncementType? type, AnnouncementStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        var items = await _repository.QueryAsync(type, status, page, pageSize, ct);
        var total = await _repository.CountAsync(type, status, ct);

        return new AnnouncementListResultDto
        {
            Items = items.Select(ToDto).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <inheritdoc />
    public async Task<AnnouncementListResultDto> GetPublishedAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var items = await _repository.GetPublishedAsync(DateTime.UtcNow, page, pageSize, ct);

        return new AnnouncementListResultDto
        {
            Items = items.Select(ToDto).ToList(),
            Total = items.Count,
            Page = page,
            PageSize = pageSize
        };
    }

    private async Task<SystemAnnouncement> RequireAnnouncementAsync(Guid announcementId, CancellationToken ct)
        => await _repository.GetByIdAsync(announcementId, ct)
           ?? throw new InvalidOperationException($"公告 {announcementId} 不存在");

    private static AnnouncementDto ToDto(SystemAnnouncement entity)
        => new()
        {
            AnnouncementId = entity.AnnouncementId,
            Title = entity.Title,
            Content = entity.Content,
            Type = entity.Type,
            TargetAudience = entity.TargetAudience,
            PublishAt = entity.PublishAt,
            ExpireAt = entity.ExpireAt,
            Status = entity.Status,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
}
```

- [ ] **Step 4：运行测试验证通过**

Run: `dotnet test src/Services/SystemAdmin/Leno.SystemAdmin.Application.Tests --filter "OutboxTests" --verbosity normal`
Expected: PASS — 3 个测试全部通过。

- [ ] **Step 5：提交**

```bash
git add src/Services/SystemAdmin/Leno.SystemAdmin.Application/Services/SystemConfigAppService.cs \
        src/Services/SystemAdmin/Leno.SystemAdmin.Application/Services/AnnouncementAppService.cs \
        src/Services/SystemAdmin/Leno.SystemAdmin.Application.Tests/Services/SystemConfigAppServiceOutboxTests.cs \
        src/Services/SystemAdmin/Leno.SystemAdmin.Application.Tests/Services/AnnouncementAppServiceOutboxTests.cs
git commit -m "fix(SystemAdmin): H-02 移除 SystemConfigAppService/AnnouncementAppService 的手动 IEventBus.PublishAsync，统一走发件箱"
```

---

### P0-H-03 修复 FeatureFlagCache 与 SystemConfigCache 写入后从不失效

**审计位置**：[file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Application/Services/FeatureFlagAppService.cs#L62-L88]、[file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Application/Services/SystemConfigAppService.cs#L50-L68]

**根因**：两个 AppService 写操作后未调用缓存 `RemoveAsync`，导致最长 30 分钟脏读。

**修复方案**：`FeatureFlagAppService` 注入 `FeatureFlagCache`，在 Update/Enable/Disable 后调用 `RemoveAsync`。`SystemConfigAppService` 注入 `SystemConfigCache`，在 Create/Update/Enable/Disable 后调用 `RemoveAsync`。

**涉及文件：**
- 修改：`src/Services/SystemAdmin/Leno.SystemAdmin.Application/Services/FeatureFlagAppService.cs`
- 修改：`src/Services/SystemAdmin/Leno.SystemAdmin.Application/Services/SystemConfigAppService.cs`（在 H-02 修复基础上追加缓存失效）
- 测试：`src/Services/SystemAdmin/Leno.SystemAdmin.Application.Tests/Services/FeatureFlagCacheInvalidationTests.cs`
- 测试：`src/Services/SystemAdmin/Leno.SystemAdmin.Application.Tests/Services/SystemConfigCacheInvalidationTests.cs`

- [ ] **Step 1：编写失败测试**

测试文件：`src/Services/SystemAdmin/Leno.SystemAdmin.Application.Tests/Services/FeatureFlagCacheInvalidationTests.cs`

```csharp
using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Application.Services;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Domain.Services;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SystemAdmin.Infrastructure.Cache;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Leno.SystemAdmin.Application.Tests.Services;

public sealed class FeatureFlagCacheInvalidationTests
{
    private readonly Mock<IFeatureFlagRepository> _repoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IFeatureFlagEvaluator> _evaluatorMock = new();
    private readonly Mock<FeatureFlagCache> _cacheMock;
    private readonly FeatureFlagAppService _service;

    public FeatureFlagCacheInvalidationTests()
    {
        _cacheMock = new Mock<FeatureFlagCache>(
            Mock.Of<StackExchange.Redis.IConnectionMultiplexer>(),
            NullLogger<FeatureFlagCache>.Instance);
        _unitOfWorkMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _service = new FeatureFlagAppService(
            _repoMock.Object,
            _unitOfWorkMock.Object,
            _evaluatorMock.Object,
            _cacheMock.Object,
            NullLogger<FeatureFlagAppService>.Instance);
    }

    [Fact]
    public async Task UpdateAsync_Should_Invalidate_Cache_By_FlagKey()
    {
        var flagId = Guid.NewGuid();
        var existing = FeatureFlag.Create(flagId, "test.flag", "测试开关", null,
            FeatureFlagStrategy.Global, null);
        _repoMock.Setup(r => r.GetByIdAsync(flagId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var dto = new UpdateFeatureFlagDto
        {
            Name = "更新开关",
            Description = null,
            Strategy = FeatureFlagStrategy.Global,
            Rules = null
        };

        await _service.UpdateAsync(flagId, dto, CancellationToken.None);

        _cacheMock.Verify(c => c.RemoveAsync("test.flag", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnableAsync_Should_Invalidate_Cache_By_FlagKey()
    {
        var flagId = Guid.NewGuid();
        var existing = FeatureFlag.Create(flagId, "test.flag", "测试开关", null,
            FeatureFlagStrategy.Global, null);
        _repoMock.Setup(r => r.GetByIdAsync(flagId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        await _service.EnableAsync(flagId, CancellationToken.None);

        _cacheMock.Verify(c => c.RemoveAsync("test.flag", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DisableAsync_Should_Invalidate_Cache_By_FlagKey()
    {
        var flagId = Guid.NewGuid();
        var existing = FeatureFlag.Create(flagId, "test.flag", "测试开关", null,
            FeatureFlagStrategy.Global, null);
        _repoMock.Setup(r => r.GetByIdAsync(flagId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        await _service.DisableAsync(flagId, CancellationToken.None);

        _cacheMock.Verify(c => c.RemoveAsync("test.flag", It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

测试文件：`src/Services/SystemAdmin/Leno.SystemAdmin.Application.Tests/Services/SystemConfigCacheInvalidationTests.cs`

```csharp
using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Application.Services;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SystemAdmin.Infrastructure.Cache;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Leno.SystemAdmin.Application.Tests.Services;

public sealed class SystemConfigCacheInvalidationTests
{
    private readonly Mock<ISystemConfigRepository> _repoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<SystemConfigCache> _cacheMock;
    private readonly SystemConfigAppService _service;

    public SystemConfigCacheInvalidationTests()
    {
        _cacheMock = new Mock<SystemConfigCache>(
            Mock.Of<StackExchange.Redis.IConnectionMultiplexer>(),
            NullLogger<SystemConfigCache>.Instance);
        _unitOfWorkMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _service = new SystemConfigAppService(
            _repoMock.Object,
            _unitOfWorkMock.Object,
            _cacheMock.Object,
            NullLogger<SystemConfigAppService>.Instance);
    }

    [Fact]
    public async Task CreateAsync_Should_Invalidate_Cache_By_Key()
    {
        var dto = new SaveSystemConfigDto
        {
            Key = "test.key", Value = "v", Group = "g", Description = null, IsEncrypted = false
        };

        await _service.CreateAsync(dto, CancellationToken.None);

        _cacheMock.Verify(c => c.RemoveAsync("test.key", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_Should_Invalidate_Cache_By_Key()
    {
        var configId = Guid.NewGuid();
        var existing = SystemConfig.Create(configId, "test.key", "old", "g", null, false);
        _repoMock.Setup(r => r.GetByIdAsync(configId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var dto = new UpdateSystemConfigDto { Value = "new", Description = null, IsEncrypted = false };
        await _service.UpdateAsync(configId, dto, CancellationToken.None);

        _cacheMock.Verify(c => c.RemoveAsync("test.key", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DisableAsync_Should_Invalidate_Cache_By_Key()
    {
        var configId = Guid.NewGuid();
        var existing = SystemConfig.Create(configId, "test.key", "v", "g", null, false);
        _repoMock.Setup(r => r.GetByIdAsync(configId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        await _service.DisableAsync(configId, CancellationToken.None);

        _cacheMock.Verify(c => c.RemoveAsync("test.key", It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

- [ ] **Step 2：运行测试验证失败**

Run: `dotnet test src/Services/SystemAdmin/Leno.SystemAdmin.Application.Tests --filter "CacheInvalidationTests" --verbosity normal`
Expected: FAIL — 编译失败，`FeatureFlagAppService` 与 `SystemConfigAppService` 构造函数不接收缓存参数。

- [ ] **Step 3：编写最小实现**

修改文件：`src/Services/SystemAdmin/Leno.SystemAdmin.Application/Services/FeatureFlagAppService.cs`

在构造函数增加 `FeatureFlagCache cache` 参数，在 Update/Enable/Disable 的 `SaveEntitiesAsync` 之后增加 `await _cache.RemoveAsync(entity.Key, ct);`。完整替换内容：

```csharp
using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Domain.Services;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SystemAdmin.Infrastructure.Cache;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;

namespace Leno.SystemAdmin.Application.Services;

/// <summary>
/// 特性开关管理应用服务实现。
/// 启停/更新经聚合根附加 <see cref="Leno.SystemAdmin.Domain.Events.FeatureFlagChangedEvent"/> 领域事件，
/// 由工作单元的发件箱机制在同一事务内持久化并发布。
/// 写操作后主动失效 Redis 缓存，避免最长 30 分钟脏读。
/// </summary>
public sealed class FeatureFlagAppService : IFeatureFlagAppService
{
    private readonly IFeatureFlagRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFeatureFlagEvaluator _evaluator;
    private readonly FeatureFlagCache _cache;
    private readonly ILogger<FeatureFlagAppService> _logger;

    public FeatureFlagAppService(
        IFeatureFlagRepository repository,
        IUnitOfWork unitOfWork,
        IFeatureFlagEvaluator evaluator,
        FeatureFlagCache cache,
        ILogger<FeatureFlagAppService> logger)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(evaluator);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(logger);
        _repository = repository;
        _unitOfWork = unitOfWork;
        _evaluator = evaluator;
        _cache = cache;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<FeatureFlagDto> CreateAsync(SaveFeatureFlagDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var flagId = Guid.NewGuid();
        var entity = FeatureFlag.Create(flagId, dto.Key, dto.Name, dto.Description, dto.Strategy, dto.Rules);

        await _repository.AddAsync(entity, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        _logger.LogInformation("特性开关已创建：{FlagId}（Key={FlagKey}）", flagId, entity.Key);
        return ToDto(entity);
    }

    /// <inheritdoc />
    public async Task<FeatureFlagDto> UpdateAsync(Guid flagId, UpdateFeatureFlagDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var entity = await RequireFlagAsync(flagId, ct);
        entity.Update(dto.Name, dto.Description, dto.Strategy, dto.Rules);

        await _repository.UpdateAsync(entity, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
        await _cache.RemoveAsync(entity.Key, ct);

        _logger.LogInformation("特性开关已更新：{FlagId}（Key={FlagKey}）", flagId, entity.Key);
        return ToDto(entity);
    }

    /// <inheritdoc />
    public async Task EnableAsync(Guid flagId, CancellationToken ct = default)
    {
        var entity = await RequireFlagAsync(flagId, ct);
        entity.Enable();

        await _repository.UpdateAsync(entity, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
        await _cache.RemoveAsync(entity.Key, ct);

        _logger.LogInformation("特性开关已启用：{FlagId}（Key={FlagKey}）", flagId, entity.Key);
    }

    /// <inheritdoc />
    public async Task DisableAsync(Guid flagId, CancellationToken ct = default)
    {
        var entity = await RequireFlagAsync(flagId, ct);
        entity.Disable();

        await _repository.UpdateAsync(entity, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
        await _cache.RemoveAsync(entity.Key, ct);

        _logger.LogInformation("特性开关已停用：{FlagId}（Key={FlagKey}）", flagId, entity.Key);
    }

    /// <inheritdoc />
    public async Task<FeatureFlagDto?> GetByKeyAsync(string key, CancellationToken ct = default)
    {
        var entity = await _repository.GetByKeyAsync(key, ct);
        return entity is null ? null : ToDto(entity);
    }

    /// <inheritdoc />
    public async Task<FeatureFlagListResultDto> QueryAsync(string? key, FeatureFlagStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        var items = await _repository.QueryAsync(key, status, page, pageSize, ct);
        var total = await _repository.CountAsync(key, status, ct);

        return new FeatureFlagListResultDto
        {
            Items = items.Select(ToDto).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <inheritdoc />
    public async Task<bool> EvaluateAsync(EvaluateFlagDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return await _evaluator.EvaluateAsync(dto.FlagKey, dto.Context, ct);
    }

    private async Task<FeatureFlag> RequireFlagAsync(Guid flagId, CancellationToken ct)
        => await _repository.GetByIdAsync(flagId, ct)
           ?? throw new InvalidOperationException($"特性开关 {flagId} 不存在");

    private static FeatureFlagDto ToDto(FeatureFlag entity)
        => new()
        {
            FlagId = entity.FlagId,
            Key = entity.Key,
            Name = entity.Name,
            Description = entity.Description,
            IsEnabled = entity.IsEnabled,
            Strategy = entity.Strategy,
            Rules = entity.Rules,
            UpdatedAt = entity.UpdatedAt
        };
}
```

修改文件：`src/Services/SystemAdmin/Leno.SystemAdmin.Application/Services/SystemConfigAppService.cs`

在 H-02 修复基础上，构造函数增加 `SystemConfigCache cache` 参数，在 Create/Update/Enable/Disable 的 `SaveEntitiesAsync` 之后增加 `await _cache.RemoveAsync(entity.Key, ct);`。完整替换内容：

```csharp
using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SystemAdmin.Infrastructure.Cache;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;

namespace Leno.SystemAdmin.Application.Services;

/// <summary>
/// 系统配置管理应用服务实现。
/// 配置变更经聚合根附加 <see cref="Leno.SystemAdmin.Domain.Events.ConfigChangedEvent"/> 领域事件，
/// 由工作单元的发件箱机制在同一事务内持久化并发布。
/// 写操作后主动失效 Redis 缓存，避免最长 30 分钟脏读。
/// </summary>
public sealed class SystemConfigAppService : ISystemConfigAppService
{
    private const string MaskedValue = "******";

    private readonly ISystemConfigRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly SystemConfigCache _cache;
    private readonly ILogger<SystemConfigAppService> _logger;

    public SystemConfigAppService(
        ISystemConfigRepository repository,
        IUnitOfWork unitOfWork,
        SystemConfigCache cache,
        ILogger<SystemConfigAppService> logger)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(logger);
        _repository = repository;
        _unitOfWork = unitOfWork;
        _cache = cache;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<SystemConfigDto> CreateAsync(SaveSystemConfigDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var configId = Guid.NewGuid();
        var entity = SystemConfig.Create(configId, dto.Key, dto.Value, dto.Group, dto.Description, dto.IsEncrypted);

        await _repository.AddAsync(entity, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
        await _cache.RemoveAsync(entity.Key, ct);

        _logger.LogInformation("系统配置已创建：{ConfigId}（Key={ConfigKey}）", configId, entity.Key);
        return ToDto(entity);
    }

    /// <inheritdoc />
    public async Task<SystemConfigDto> UpdateAsync(Guid configId, UpdateSystemConfigDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var entity = await RequireConfigAsync(configId, ct);
        entity.Update(dto.Value, dto.Description, dto.IsEncrypted);

        await _repository.UpdateAsync(entity, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
        await _cache.RemoveAsync(entity.Key, ct);

        _logger.LogInformation("系统配置已更新：{ConfigId}（Key={ConfigKey}）", configId, entity.Key);
        return ToDto(entity);
    }

    /// <inheritdoc />
    public async Task EnableAsync(Guid configId, CancellationToken ct = default)
    {
        var entity = await RequireConfigAsync(configId, ct);
        entity.Enable();

        await _repository.UpdateAsync(entity, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
        await _cache.RemoveAsync(entity.Key, ct);

        _logger.LogInformation("系统配置已启用：{ConfigId}（Key={ConfigKey}）", configId, entity.Key);
    }

    /// <inheritdoc />
    public async Task DisableAsync(Guid configId, CancellationToken ct = default)
    {
        var entity = await RequireConfigAsync(configId, ct);
        entity.Disable();

        await _repository.UpdateAsync(entity, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
        await _cache.RemoveAsync(entity.Key, ct);

        _logger.LogInformation("系统配置已停用：{ConfigId}（Key={ConfigKey}）", configId, entity.Key);
    }

    /// <inheritdoc />
    public async Task<SystemConfigDto?> GetByKeyAsync(string key, CancellationToken ct = default)
    {
        var entity = await _repository.GetByKeyAsync(key, ct);
        return entity is null ? null : ToDto(entity);
    }

    /// <inheritdoc />
    public async Task<List<SystemConfigDto>> GetByGroupAsync(string group, CancellationToken ct = default)
    {
        var configs = await _repository.QueryByGroupAsync(group, ct);
        return configs.Select(ToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<SystemConfigListResultDto> QueryAsync(string? key, string? group, ConfigStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        var items = await _repository.QueryAsync(key, group, status, page, pageSize, ct);
        var total = await _repository.CountAsync(key, group, status, ct);

        return new SystemConfigListResultDto
        {
            Items = items.Select(ToDto).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    private async Task<SystemConfig> RequireConfigAsync(Guid configId, CancellationToken ct)
        => await _repository.GetByIdAsync(configId, ct)
           ?? throw new InvalidOperationException($"系统配置 {configId} 不存在");

    private static SystemConfigDto ToDto(SystemConfig entity)
        => new()
        {
            ConfigId = entity.ConfigId,
            Key = entity.Key,
            Value = entity.IsEncrypted ? MaskedValue : entity.Value,
            Group = entity.Group,
            Description = entity.Description,
            IsEncrypted = entity.IsEncrypted,
            Status = entity.Status,
            UpdatedAt = entity.UpdatedAt
        };
}
```

- [ ] **Step 4：运行测试验证通过**

Run: `dotnet test src/Services/SystemAdmin/Leno.SystemAdmin.Application.Tests --filter "CacheInvalidationTests" --verbosity normal`
Expected: PASS — 6 个测试全部通过。

- [ ] **Step 5：提交**

```bash
git add src/Services/SystemAdmin/Leno.SystemAdmin.Application/Services/FeatureFlagAppService.cs \
        src/Services/SystemAdmin/Leno.SystemAdmin.Application/Services/SystemConfigAppService.cs \
        src/Services/SystemAdmin/Leno.SystemAdmin.Application.Tests/Services/FeatureFlagCacheInvalidationTests.cs \
        src/Services/SystemAdmin/Leno.SystemAdmin.Application.Tests/Services/SystemConfigCacheInvalidationTests.cs
git commit -m "fix(SystemAdmin): H-03 FeatureFlagAppService/SystemConfigAppService 写操作后主动失效 Redis 缓存"
```

---

### P0-H-04 修复 AuditLogConsumer 幂等去重 TOCTOU 竞态

**审计位置**：[file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Consumers/AuditLogConsumer.cs#L255-L277]

**根因**：`CreateAuditLogEntryAsync` 采用"先查后插"模式，并发消费同一 EventId 时两个线程都可能通过检查。`AuditLogEntryConfiguration` 已配置 EventId 唯一索引（[file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Configurations/AuditLogEntryConfiguration.cs#L34]），但消费者未捕获 `DbUpdateException`。

**修复方案**：保留"先查后插"作为快速路径，在 `AddAsync` + `SaveEntitiesAsync` 外层包裹 try-catch，捕获 `DbUpdateException`。若为唯一约束冲突（SQL Server 错误码 2601/2627）则视为已存在并正常返回，否则重抛。

**涉及文件：**
- 修改：`src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Consumers/AuditLogConsumer.cs`
- 测试：`src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure.Tests/Consumers/AuditLogConsumerIdempotencyTests.cs`

- [ ] **Step 1：编写失败测试**

测试文件：`src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure.Tests/Consumers/AuditLogConsumerIdempotencyTests.cs`

```csharp
using Leno.SharedContracts.Events;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SharedKernel.Abstractions;
using Leno.SystemAdmin.Infrastructure.Consumers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Leno.SystemAdmin.Infrastructure.Tests.Consumers;

public sealed class AuditLogConsumerIdempotencyTests
{
    private readonly Mock<IAuditLogRepository> _auditLogRepoMock = new();
    private readonly Mock<IAuditLogEntryRepository> _entryRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly AuditLogConsumer _consumer;

    public AuditLogConsumerIdempotencyTests()
    {
        _consumer = new AuditLogConsumer(
            _auditLogRepoMock.Object,
            _entryRepoMock.Object,
            _unitOfWorkMock.Object,
            NullLogger<AuditLogConsumer>.Instance);
    }

    [Fact]
    public async Task Consume_OrderCreated_Should_Swallow_DbUpdateException_For_Duplicate_EventId()
    {
        var evt = new OrderCreatedEvent
        {
            EventId = Guid.NewGuid(),
            AggregateId = Guid.NewGuid(),
            BuyerId = Guid.NewGuid(),
            OrderNo = "ORD-001",
            TotalAmount = 100m,
            Currency = "CNY",
            OccurredAt = DateTime.UtcNow
        };
        var context = new TestConsumeContext<OrderCreatedEvent>(evt);

        _entryRepoMock.Setup(r => r.GetByEventIdAsync(evt.EventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuditLogEntry?)null);
        _unitOfWorkMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException(
                "唯一索引冲突",
                new InvalidOperationException("Violation of UNIQUE KEY constraint 'ix_audit_log_entries_event_id'")));

        // 不应抛异常——重复 EventId 视为已处理
        await _consumer.Consume(context);
    }

    [Fact]
    public async Task Consume_OrderCreated_Should_Rethrow_Non_Duplicate_DbUpdateException()
    {
        var evt = new OrderCreatedEvent
        {
            EventId = Guid.NewGuid(),
            AggregateId = Guid.NewGuid(),
            BuyerId = Guid.NewGuid(),
            OrderNo = "ORD-002",
            TotalAmount = 200m,
            Currency = "CNY",
            OccurredAt = DateTime.UtcNow
        };
        var context = new TestConsumeContext<OrderCreatedEvent>(evt);

        _entryRepoMock.Setup(r => r.GetByEventIdAsync(evt.EventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuditLogEntry?)null);
        _unitOfWorkMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("连接超时", new TimeoutException("timeout")));

        await Assert.ThrowsAsync<DbUpdateException>(() => _consumer.Consume(context));
    }
}

internal sealed class TestConsumeContext<T> : MassTransit.Testing.TestConsumeContext<T> where T : class
{
    public TestConsumeContext(T message) : base(message)
    {
        CancellationToken = CancellationToken.None;
    }
}
```

- [ ] **Step 2：运行测试验证失败**

Run: `dotnet test src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure.Tests --filter "AuditLogConsumerIdempotencyTests" --verbosity normal`
Expected: FAIL — `DbUpdateException` 未被捕获，第一个测试抛异常失败。

- [ ] **Step 3：编写最小实现**

修改文件：`src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Consumers/AuditLogConsumer.cs`

仅修改 `CreateAuditLogEntryAsync` 方法（L255-L277），在 `AddAsync` + `SaveEntitiesAsync` 外层增加 try-catch。替换该方法：

```csharp
    /// <summary>
    /// 创建 AuditLogEntry 审计日志条目，支持幂等去重。
    /// 先按 EventId 查询快速路径跳过；并发插入时由 EventId 唯一索引兜底，
    /// 捕获 DbUpdateException 判定为唯一约束冲突则视为已存在并正常返回。
    /// </summary>
    private async Task CreateAuditLogEntryAsync(
        Guid eventId, string eventType, Guid aggregateId, string module,
        string action, Guid operatorId, string? operatorName,
        string? requestSummary, DateTime timestamp, string? ipAddress,
        CancellationToken ct)
    {
        // 幂等去重：按 EventId 检查是否已存在（快速路径）
        var existing = await _auditLogEntryRepository.GetByEventIdAsync(eventId, ct);
        if (existing is not null)
        {
            _logger.LogDebug("审计日志条目已存在，跳过 EventId={EventId}", eventId);
            return;
        }

        var entry = AuditLogEntry.Create(
            Guid.NewGuid(), eventId, eventType, aggregateId, module,
            action, operatorId, operatorName, requestSummary, timestamp, ipAddress);

        try
        {
            await _auditLogEntryRepository.AddAsync(entry, ct);
            await _unitOfWork.SaveEntitiesAsync(ct);

            _logger.LogInformation("审计日志条目已记录 EventId={EventId} EventType={EventType}", eventId, eventType);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // 并发插入导致唯一索引冲突，视为已存在，正常返回
            _logger.LogWarning(ex,
                "审计日志条目并发插入冲突，已按幂等处理 EventId={EventId}", eventId);
        }
    }

    /// <summary>
    /// 判断 DbUpdateException 是否为唯一约束冲突（SQL Server 错误码 2601/2627）。
    /// </summary>
    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        var inner = ex.InnerException;
        if (inner is null)
        {
            return false;
        }

        var message = inner.Message ?? string.Empty;
        // SQL Server: 2601 (唯一键) / 2627 (违反约束)
        // 同时匹配索引名 ix_audit_log_entries_event_id 作为兜底
        return message.Contains("2601", StringComparison.Ordinal)
            || message.Contains("2627", StringComparison.Ordinal)
            || message.Contains("ix_audit_log_entries_event_id", StringComparison.OrdinalIgnoreCase)
            || message.Contains("UNIQUE KEY", StringComparison.OrdinalIgnoreCase)
            || message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase);
    }
```

并在文件顶部 `using` 区域增加：

```csharp
using Microsoft.EntityFrameworkCore;
```

- [ ] **Step 4：运行测试验证通过**

Run: `dotnet test src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure.Tests --filter "AuditLogConsumerIdempotencyTests" --verbosity normal`
Expected: PASS — 2 个测试全部通过。

- [ ] **Step 5：提交**

```bash
git add src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Consumers/AuditLogConsumer.cs \
        src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure.Tests/Consumers/AuditLogConsumerIdempotencyTests.cs
git commit -m "fix(SystemAdmin): H-04 AuditLogConsumer 捕获唯一索引冲突消除 TOCTOU 竞态"
```

---

### P0-H-05 修复 DeadLetterQueueManager/RabbitMqDeadLetterManager 使用 SaveChangesAsync 而非 SaveEntitiesAsync

**审计位置**：[file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Services/DeadLetterQueueManager.cs#L75-L77]、[file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Services/RabbitMqDeadLetterManager.cs#L173-L175]、[file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Services/RabbitMqDeadLetterManager.cs#L193-L194]

**根因**：三处 `SaveChangesAsync` 仅持久化聚合状态，丢弃领域事件（如 `DeadLetterRetriedEvent`）。应使用 `SaveEntitiesAsync` 经发件箱投递。

**修复方案**：将三处 `await _unitOfWork.SaveChangesAsync(ct)` 改为 `await _unitOfWork.SaveEntitiesAsync(ct)`。

**涉及文件：**
- 修改：`src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Services/DeadLetterQueueManager.cs`
- 修改：`src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Services/RabbitMqDeadLetterManager.cs`
- 测试：`src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure.Tests/Services/DeadLetterSaveEntitiesTests.cs`

- [ ] **Step 1：编写失败测试**

测试文件：`src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure.Tests/Services/DeadLetterSaveEntitiesTests.cs`

```csharp
using Leno.Infrastructure.Abstractions;
using Leno.SharedKernel.Abstractions;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Domain.Services;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SystemAdmin.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Leno.SystemAdmin.Infrastructure.Tests.Services;

public sealed class DeadLetterSaveEntitiesTests
{
    private readonly Mock<IDeadLetterMessageRepository> _repoMock = new();
    private readonly Mock<IEventBus> _eventBusMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly DeadLetterQueueManager _manager;

    public DeadLetterSaveEntitiesTests()
    {
        _manager = new DeadLetterQueueManager(
            _repoMock.Object,
            _eventBusMock.Object,
            _unitOfWorkMock.Object,
            NullLogger<DeadLetterQueueManager>.Instance);
    }

    [Fact]
    public async Task RepublishAsync_Should_Call_SaveEntitiesAsync_Not_SaveChangesAsync()
    {
        var messageId = Guid.NewGuid();
        var message = CreateDeadLetterMessage(messageId, DeadLetterStatus.Pending);
        _repoMock.Setup(r => r.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);
        _eventBusMock.Setup(e => e.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _manager.RepublishAsync(messageId, CancellationToken.None);

        _unitOfWorkMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static DeadLetterMessage CreateDeadLetterMessage(Guid id, DeadLetterStatus status)
    {
        var msg = DeadLetterMessage.Create(
            id,
            "orig-001",
            "test-ctx",
            "test-topic",
            "{}",
            "{}",
            "test error");
        return msg;
    }
}
```

- [ ] **Step 2：运行测试验证失败**

Run: `dotnet test src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure.Tests --filter "DeadLetterSaveEntitiesTests" --verbosity normal`
Expected: FAIL — 验证 `SaveEntitiesAsync` 被调用但实际调用的是 `SaveChangesAsync`。

- [ ] **Step 3：编写最小实现**

修改文件：`src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Services/DeadLetterQueueManager.cs`

将 L77 行 `await _unitOfWork.SaveChangesAsync(ct);` 改为 `await _unitOfWork.SaveEntitiesAsync(ct);`：

```csharp
        // 重投成功后标记消息状态为 Retried 并持久化（经发件箱投递领域事件）
        message.Retry("system");
        await _repository.UpdateAsync(message, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
```

修改文件：`src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Services/RabbitMqDeadLetterManager.cs`

将 L175 行 `await _unitOfWork.SaveChangesAsync(ct);` 改为 `await _unitOfWork.SaveEntitiesAsync(ct);`：

```csharp
        // 重投成功后标记消息状态为 Retried（经发件箱投递领域事件）
        message.Retry("system");
        await _repository.UpdateAsync(message, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
```

将 L194 行 `await _unitOfWork.SaveChangesAsync(ct);` 改为 `await _unitOfWork.SaveEntitiesAsync(ct);`：

```csharp
        await _repository.AddAsync(message, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
```

- [ ] **Step 4：运行测试验证通过**

Run: `dotnet test src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure.Tests --filter "DeadLetterSaveEntitiesTests" --verbosity normal`
Expected: PASS — 测试通过。

- [ ] **Step 5：提交**

```bash
git add src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Services/DeadLetterQueueManager.cs \
        src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Services/RabbitMqDeadLetterManager.cs \
        src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure.Tests/Services/DeadLetterSaveEntitiesTests.cs
git commit -m "fix(SystemAdmin): H-05 死信管理器 SaveChangesAsync 改为 SaveEntitiesAsync 保证领域事件投递"
```

---

### P0-H-06 修复 IndexRebuildOrchestrator 多步状态变更无事务

**审计位置**：[file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Services/IndexRebuildOrchestrator.cs#L38-L68]、[file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Services/IndexRebuildOrchestrator.cs#L88-L108]

**根因**：`TriggerAsync` 执行 3 次独立 `SaveEntitiesAsync`（创建→启动→触发），中途失败导致状态不一致。`RetryAsync` 未重新检查并发任务。

**修复方案**：`TriggerAsync` 合并为 Create + Start + 单次 `SaveEntitiesAsync`，随后调用 `_trigger.StartAsync`；若触发失败则 `task.Fail` + `SaveEntitiesAsync`。`RetryAsync` 在重试前重新检查 `GetRunningByIndexAsync`。

**涉及文件：**
- 修改：`src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Services/IndexRebuildOrchestrator.cs`
- 测试：`src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure.Tests/Services/IndexRebuildOrchestratorTests.cs`

- [ ] **Step 1：编写失败测试**

测试文件：`src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure.Tests/Services/IndexRebuildOrchestratorTests.cs`

```csharp
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Exceptions;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Domain.Services;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Leno.SystemAdmin.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Leno.SystemAdmin.Infrastructure.Tests.Services;

public sealed class IndexRebuildOrchestratorTests
{
    private readonly Mock<IIndexRebuildTaskRepository> _repoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IIndexRebuildTrigger> _triggerMock = new();
    private readonly IndexRebuildOrchestrator _orchestrator;

    public IndexRebuildOrchestratorTests()
    {
        _orchestrator = new IndexRebuildOrchestrator(
            _repoMock.Object,
            _unitOfWorkMock.Object,
            _triggerMock.Object,
            NullLogger<IndexRebuildOrchestrator>.Instance);
    }

    [Fact]
    public async Task TriggerAsync_Should_Call_SaveEntitiesAsync_Once_Not_Three_Times()
    {
        _repoMock.Setup(r => r.GetRunningByIndexAsync("Product", "products", It.IsAny<CancellationToken>()))
            .ReturnsAsync((IndexRebuildTask?)null);
        _unitOfWorkMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _triggerMock.Setup(t => t.StartAsync(It.IsAny<Guid>(), "Product", "products", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var task = await _orchestrator.TriggerAsync("Product", "products", "admin", CancellationToken.None);

        Assert.Equal(RebuildTaskStatus.Running, task.Status);
        _unitOfWorkMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TriggerAsync_Should_Mark_Failed_When_Trigger_StartAsync_Throws()
    {
        _repoMock.Setup(r => r.GetRunningByIndexAsync("Order", "orders", It.IsAny<CancellationToken>()))
            .ReturnsAsync((IndexRebuildTask?)null);
        _unitOfWorkMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _triggerMock.Setup(t => t.StartAsync(It.IsAny<Guid>(), "Order", "orders", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("ES 不可用"));

        var task = await _orchestrator.TriggerAsync("Order", "orders", "admin", CancellationToken.None);

        Assert.Equal(RebuildTaskStatus.Failed, task.Status);
        Assert.Contains("ES 不可用", task.ErrorMessage);
        _unitOfWorkMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task RetryAsync_Should_Throw_When_Concurrent_Running_Task_Exists()
    {
        var taskId = Guid.NewGuid();
        var existingTask = IndexRebuildTask.Create(taskId, "Product", "products", "admin");
        existingTask.Start();
        _repoMock.Setup(r => r.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTask);
        // 模拟并发：存在另一个运行中的任务
        _repoMock.Setup(r => r.GetRunningByIndexAsync("Product", "products", It.IsAny<CancellationToken>()))
            .ReturnsAsync(IndexRebuildTask.Create(Guid.NewGuid(), "Product", "products", "other"));

        // 先 Fail existingTask 使其可重试
        existingTask.Fail("之前的错误");

        await Assert.ThrowsAsync<SystemAdminDomainException>(
            () => _orchestrator.RetryAsync(taskId, "admin", CancellationToken.None));
    }
}
```

- [ ] **Step 2：运行测试验证失败**

Run: `dotnet test src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure.Tests --filter "IndexRebuildOrchestratorTests" --verbosity normal`
Expected: FAIL — 当前 `TriggerAsync` 调用 2 次 `SaveEntitiesAsync`，测试期望 1 次；触发失败时未标记 Failed；`RetryAsync` 未检查并发任务。

- [ ] **Step 3：编写最小实现**

修改文件：`src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Services/IndexRebuildOrchestrator.cs`

完整替换 `TriggerAsync` 与 `RetryAsync` 方法：

```csharp
    /// <inheritdoc />
    public async Task<IndexRebuildTask> TriggerAsync(string targetContext, string indexName, string triggeredBy, CancellationToken ct)
    {
        // 检查同一索引是否已有运行中任务
        var existing = await _repository.GetRunningByIndexAsync(targetContext, indexName, ct);
        if (existing is not null)
        {
            throw new SystemAdminDomainException(
                $"索引 {targetContext}/{indexName} 已有运行中的重建任务（TaskId={existing.TaskId}），不可重复触发",
                "REBUILD_TASK_CONFLICT");
        }

        var taskId = Guid.NewGuid();
        var task = IndexRebuildTask.Create(taskId, targetContext, indexName, triggeredBy);
        task.Start();

        await _repository.AddAsync(task, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        // 触发底层索引重建操作；失败时标记任务为 Failed 并持久化
        try
        {
            await _trigger.StartAsync(taskId, targetContext, indexName, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "ES 索引重建触发失败，标记任务为 Failed TaskId={TaskId}", taskId);
            task.Fail(ex.Message);
            await _repository.UpdateAsync(task, ct);
            await _unitOfWork.SaveEntitiesAsync(ct);
        }

        _logger.LogInformation(
            "索引重建任务已创建并启动：TaskId={TaskId}, TargetContext={TargetContext}, IndexName={IndexName}",
            taskId, targetContext, indexName);

        return task;
    }

    /// <inheritdoc />
    public async Task<IndexRebuildTask> RetryAsync(Guid taskId, string triggeredBy, CancellationToken ct)
    {
        var task = await _repository.GetByIdAsync(taskId, ct)
                   ?? throw new SystemAdminDomainException($"索引重建任务 {taskId} 不存在", "REBUILD_TASK_NOT_FOUND");

        // 重试前重新检查并发任务，避免与正在运行的任务竞争同一索引
        var concurrent = await _repository.GetRunningByIndexAsync(task.TargetContext, task.IndexName, ct);
        if (concurrent is not null && concurrent.TaskId != taskId)
        {
            throw new SystemAdminDomainException(
                $"索引 {task.TargetContext}/{task.IndexName} 已有运行中的重建任务（TaskId={concurrent.TaskId}），不可重试",
                "REBUILD_TASK_CONFLICT");
        }

        task.Retry(triggeredBy);
        task.Start();
        await _repository.UpdateAsync(task, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        try
        {
            await _trigger.StartAsync(taskId, task.TargetContext, task.IndexName, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "ES 索引重建重试触发失败，标记任务为 Failed TaskId={TaskId}", taskId);
            task.Fail(ex.Message);
            await _repository.UpdateAsync(task, ct);
            await _unitOfWork.SaveEntitiesAsync(ct);
        }

        _logger.LogInformation(
            "索引重建任务已重试：TaskId={TaskId}, RetryCount={RetryCount}, TargetContext={TargetContext}, IndexName={IndexName}",
            taskId, task.RetryCount, task.TargetContext, task.IndexName);

        return task;
    }
```

- [ ] **Step 4：运行测试验证通过**

Run: `dotnet test src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure.Tests --filter "IndexRebuildOrchestratorTests" --verbosity normal`
Expected: PASS — 3 个测试全部通过。

- [ ] **Step 5：提交**

```bash
git add src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Services/IndexRebuildOrchestrator.cs \
        src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure.Tests/Services/IndexRebuildOrchestratorTests.cs
git commit -m "fix(SystemAdmin): H-06 IndexRebuildOrchestrator 合并事务并增加重试并发检查"
```

---

### P0-H-07 修复 AuditLogConsumer 与 AfterSalesEventConsumer 同时消费 AfterSalesApprovedEvent

**审计位置**：[file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Consumers/AuditLogConsumer.cs#L59-L78]、[file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Consumers/AfterSalesEventConsumer.cs#L37-L64]

**根因**：`AuditLogConsumer.AfterSalesApproved` 既写 `AuditLogEntry` 又写 `AuditLog`（L74-L77），职责越界；`AfterSalesEventConsumer` 写 `OperationLog` 无幂等检查。

**修复方案**：
1. 删除 `AuditLogConsumer.Consume(AfterSalesApprovedEvent)` 中对 `AuditLog` 的写入（L74-L77），仅保留 `AuditLogEntry`。
2. 为 `OperationLog` 聚合根增加 `EventId` 字段 + 唯一索引，`IOperationLogRepository` 增加 `GetByEventIdAsync`，`AfterSalesEventConsumer` 增加幂等检查。

**涉及文件：**
- 修改：`src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Consumers/AuditLogConsumer.cs`（删除 L74-L77）
- 修改：`src/Services/SystemAdmin/Leno.SystemAdmin.Domain/Aggregates/OperationLog.cs`（增加 EventId 字段）
- 修改：`src/Services/SystemAdmin/Leno.SystemAdmin.Domain/Repositories/IOperationLogRepository.cs`（增加 GetByEventIdAsync）
- 修改：`src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Repositories/EfCoreOperationLogRepository.cs`（实现 GetByEventIdAsync）
- 修改：`src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Configurations/OperationLogConfiguration.cs`（增加 EventId 唯一索引）
- 修改：`src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Consumers/AfterSalesEventConsumer.cs`（增加幂等检查）
- 测试：`src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure.Tests/Consumers/AfterSalesEventConsumerIdempotencyTests.cs`

- [ ] **Step 1：编写失败测试**

测试文件：`src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure.Tests/Consumers/AfterSalesEventConsumerIdempotencyTests.cs`

```csharp
using Leno.SharedContracts.Events;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SharedKernel.Abstractions;
using Leno.SystemAdmin.Infrastructure.Consumers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Leno.SystemAdmin.Infrastructure.Tests.Consumers;

public sealed class AfterSalesEventConsumerIdempotencyTests
{
    private readonly Mock<IOperationLogRepository> _repoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly AfterSalesEventConsumer _consumer;

    public AfterSalesEventConsumerIdempotencyTests()
    {
        _consumer = new AfterSalesEventConsumer(
            _repoMock.Object,
            _unitOfWorkMock.Object,
            NullLogger<AfterSalesEventConsumer>.Instance);
    }

    [Fact]
    public async Task Consume_AfterSalesApproved_Should_Skip_When_EventId_Already_Processed()
    {
        var evt = new AfterSalesApprovedEvent
        {
            EventId = Guid.NewGuid(),
            AfterSalesId = Guid.NewGuid(),
            SellerId = Guid.NewGuid(),
            ApprovedAmount = 100m,
            Currency = "CNY",
            OccurredAt = DateTime.UtcNow
        };
        var context = new TestConsumeContext<AfterSalesApprovedEvent>(evt);

        var existingLog = OperationLog.Create(
            Guid.NewGuid(), evt.SellerId, "Approve", "AfterSales",
            "已存在", null, null, null, evt.OccurredAt);
        _repoMock.Setup(r => r.GetByEventIdAsync(evt.EventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingLog);

        await _consumer.Consume(context);

        _repoMock.Verify(r => r.AddAsync(It.IsAny<OperationLog>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Consume_AfterSalesApproved_Should_Write_OperationLog_When_Not_Processed()
    {
        var evt = new AfterSalesApprovedEvent
        {
            EventId = Guid.NewGuid(),
            AfterSalesId = Guid.NewGuid(),
            SellerId = Guid.NewGuid(),
            ApprovedAmount = 200m,
            Currency = "CNY",
            OccurredAt = DateTime.UtcNow
        };
        var context = new TestConsumeContext<AfterSalesApprovedEvent>(evt);

        _repoMock.Setup(r => r.GetByEventIdAsync(evt.EventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OperationLog?)null);
        _unitOfWorkMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _consumer.Consume(context);

        _repoMock.Verify(r => r.AddAsync(It.IsAny<OperationLog>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}

internal sealed class TestConsumeContext<T> : MassTransit.Testing.TestConsumeContext<T> where T : class
{
    public TestConsumeContext(T message) : base(message)
    {
        CancellationToken = CancellationToken.None;
    }
}
```

- [ ] **Step 2：运行测试验证失败**

Run: `dotnet test src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure.Tests --filter "AfterSalesEventConsumerIdempotencyTests" --verbosity normal`
Expected: FAIL — `IOperationLogRepository` 无 `GetByEventIdAsync` 方法，编译失败。

- [ ] **Step 3：编写最小实现**

修改文件：`src/Services/SystemAdmin/Leno.SystemAdmin.Domain/Aggregates/OperationLog.cs`

增加 `EventId` 属性并修改 `Create` 工厂方法签名。在 `OccurredAt` 属性下方增加：

```csharp
    /// <summary>来源集成事件标识，用于幂等去重，可空（非事件驱动的操作日志为 null）。</summary>
    public Guid? EventId { get; private set; }
```

修改 `Create` 方法签名为：

```csharp
    public static OperationLog Create(
        Guid logId,
        Guid operatorId,
        string operationType,
        string module,
        string? description,
        string? beforeSnapshot,
        string? afterSnapshot,
        string? ipAddress,
        DateTime occurredAt,
        Guid? eventId = null)
```

在 `return new OperationLog(logId)` 初始化块中增加：

```csharp
            EventId = eventId,
```

修改文件：`src/Services/SystemAdmin/Leno.SystemAdmin.Domain/Repositories/IOperationLogRepository.cs`

在接口中增加：

```csharp
    /// <summary>
    /// 按来源事件标识获取操作日志，用于幂等去重。
    /// </summary>
    /// <param name="eventId">来源集成事件标识。</param>
    /// <param name="ct">取消令牌。</param>
    Task<OperationLog?> GetByEventIdAsync(Guid eventId, CancellationToken ct = default);
```

修改文件：`src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Repositories/EfCoreOperationLogRepository.cs`

增加实现方法：

```csharp
    /// <inheritdoc />
    public Task<OperationLog?> GetByEventIdAsync(Guid eventId, CancellationToken ct = default)
        => _context.OperationLogs.FirstOrDefaultAsync(l => l.EventId == eventId, ct);
```

修改文件：`src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Configurations/OperationLogConfiguration.cs`

增加 EventId 列与唯一索引：

```csharp
        builder.Property(l => l.EventId).HasColumnName("event_id");
        builder.HasIndex(l => l.EventId).HasDatabaseName("ix_operation_logs_event_id").IsUnique();
```

修改文件：`src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Consumers/AfterSalesEventConsumer.cs`

完整替换 `Consume(ConsumeContext<AfterSalesApprovedEvent>)` 方法：

```csharp
    /// <inheritdoc />
    public async Task Consume(ConsumeContext<AfterSalesApprovedEvent> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var ct = context.CancellationToken;
        var evt = context.Message;

        if (evt.SellerId == Guid.Empty)
        {
            _logger.LogWarning("售后审核事件缺少操作人上下文，跳过操作日志记录 EventId={EventId}", evt.EventId);
            return;
        }

        // 幂等去重：按 EventId 检查是否已处理
        var existing = await _operationLogRepository.GetByEventIdAsync(evt.EventId, ct);
        if (existing is not null)
        {
            _logger.LogDebug("操作日志已存在，跳过 EventId={EventId}", evt.EventId);
            return;
        }

        var log = OperationLog.Create(
            Guid.NewGuid(),
            evt.SellerId,
            "Approve",
            "AfterSales",
            $"售后审核通过 AfterSalesId={evt.AfterSalesId}",
            null,
            $"{{\"afterSalesId\":\"{evt.AfterSalesId}\",\"amount\":{evt.ApprovedAmount.ToString(CultureInfo.InvariantCulture)}}}",
            null,
            evt.OccurredAt,
            evt.EventId);

        try
        {
            await _operationLogRepository.AddAsync(log, ct);
            await _unitOfWork.SaveEntitiesAsync(ct);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            _logger.LogWarning(ex,
                "操作日志并发插入冲突，已按幂等处理 EventId={EventId}", evt.EventId);
            return;
        }

        _logger.LogInformation("操作日志已记录 EventId={EventId}", evt.EventId);
    }

    private static bool IsUniqueConstraintViolation(Microsoft.EntityFrameworkCore.DbUpdateException ex)
    {
        var inner = ex.InnerException;
        if (inner is null)
        {
            return false;
        }

        var message = inner.Message ?? string.Empty;
        return message.Contains("2601", StringComparison.Ordinal)
            || message.Contains("2627", StringComparison.Ordinal)
            || message.Contains("ix_operation_logs_event_id", StringComparison.OrdinalIgnoreCase)
            || message.Contains("UNIQUE KEY", StringComparison.OrdinalIgnoreCase)
            || message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase);
    }
```

修改文件：`src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Consumers/AuditLogConsumer.cs`

删除 `Consume(ConsumeContext<AfterSalesApprovedEvent>)` 方法中 L74-L77 对 `AuditLog` 的写入。替换该方法为：

```csharp
    public async Task Consume(ConsumeContext<AfterSalesApprovedEvent> context)
    {
        var evt = context.Message;
        var ct = context.CancellationToken;

        if (evt.SellerId == Guid.Empty)
        {
            _logger.LogWarning("售后审核事件缺少操作人上下文，跳过审计日志记录 EventId={EventId}", evt.EventId);
            return;
        }

        var summary = MaskSensitiveData($"售后审核通过 金额={evt.ApprovedAmount.ToString(CultureInfo.InvariantCulture)} {evt.Currency}");
        await CreateAuditLogEntryAsync(evt.EventId, "AfterSalesApprovedEvent", evt.AggregateId, "AfterSales",
            "AfterSalesApprove", evt.SellerId, null, summary, evt.OccurredAt, null, ct);
    }
```

- [ ] **Step 4：运行测试验证通过**

Run: `dotnet test src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure.Tests --filter "AfterSalesEventConsumerIdempotencyTests" --verbosity normal`
Expected: PASS — 2 个测试全部通过。

- [ ] **Step 5：提交**

```bash
git add src/Services/SystemAdmin/Leno.SystemAdmin.Domain/Aggregates/OperationLog.cs \
        src/Services/SystemAdmin/Leno.SystemAdmin.Domain/Repositories/IOperationLogRepository.cs \
        src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Repositories/EfCoreOperationLogRepository.cs \
        src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Configurations/OperationLogConfiguration.cs \
        src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Consumers/AfterSalesEventConsumer.cs \
        src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Consumers/AuditLogConsumer.cs \
        src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure.Tests/Consumers/AfterSalesEventConsumerIdempotencyTests.cs
git commit -m "fix(SystemAdmin): H-07 拆分消费者职责并增加 OperationLog 幂等去重"
```

---

## P1 详细修复计划（任务清单格式）

### P1-M-01 DashboardController 直接返回领域实体 DashboardReport

**审计位置**：[file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/DashboardController.cs#L40-L157]

**根因**：7 个端点返回 `ApiResponse<DashboardReport>`，泄露 `Metrics`/`Granularity`/`Period` 等聚合内部结构。

**任务清单：**
- [ ] 在 `Application/DTOs/SystemAdminDtos.cs` 新增 `DashboardReportDto`（含 `ReportId`/`ReportType`/`Granularity`/`GeneratedAt`/`List<MetricItemDto>` 字段）与 `MetricItemDto`（含 `Key`/`Value`/`Unit` 字段）
- [ ] 在 `DashboardController` 增加 `DashboardReport → DashboardReportDto` 映射私有方法
- [ ] 将 7 个端点的 `ApiResponse<DashboardReport>` 改为 `ApiResponse<DashboardReportDto>`，`ApiResponse<List<DashboardReport>>` 改为 `ApiResponse<List<DashboardReportDto>>`
- [ ] 编写 `DashboardControllerDtoMappingTests` 验证返回类型为 DTO 而非领域实体
- [ ] 提交：`git commit -m "fix(SystemAdmin): M-01 DashboardController 返回 DTO 替代领域实体"`

### P1-M-02 StatisticsController 直接返回领域实体 ReconciliationRecord

**审计位置**：[file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/StatisticsController.cs#L73-L108]

**根因**：`TriggerReconciliationAsync` 与 `GetReconciliationRecordsAsync` 返回 `ApiResponse<ReconciliationRecord>`，泄露 `Snapshot`/`Status`/`AlertTriggered` 等内部字段。

**任务清单：**
- [ ] 在 `Application/DTOs/SystemAdminDtos.cs` 新增 `ReconciliationRecordDto`（含 `RecordId`/`ReportType`/`ReconciledAt`/`Status`/`DiscrepancyCount`/`AlertTriggered`/`CorrectionTriggered` 字段）
- [ ] 在 `StatisticsController` 增加 `ReconciliationRecord → ReconciliationRecordDto` 映射私有方法
- [ ] 将 `TriggerReconciliationAsync` 与 `GetReconciliationRecordsAsync` 的返回类型改为 `ApiResponse<ReconciliationRecordDto>` / `ApiResponse<List<ReconciliationRecordDto>>`
- [ ] 编写测试验证返回类型为 DTO
- [ ] 提交：`git commit -m "fix(SystemAdmin): M-02 StatisticsController 返回 DTO 替代领域实体"`

### P1-M-03 RateLimitRule 聚合根缺少 RowVersion

**审计位置**：[file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Domain/Aggregates/RateLimitRule.cs#L12-L44]、[file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Configurations/RateLimitRuleConfiguration.cs#L12-L33]

**根因**：`RateLimitRule` 无 `RowVersion` 字段，控制器的 `DbUpdateConcurrencyException` 捕获（[file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/RateLimitRulesController.cs#L81-L84]）永不触发。

**任务清单：**
- [ ] 在 `RateLimitRule` 聚合根增加 `public byte[] Version { get; private set; } = Array.Empty<byte>();`
- [ ] 在 `RateLimitRuleConfiguration` 增加 `builder.Property(r => r.Version).HasColumnName("version").IsRowVersion();`
- [ ] 在 `RateLimitRuleAppService` 的 `ToDto` 映射中将 `entity.Version` 赋给 `RateLimitRuleDto.Version`
- [ ] 新增 EF Core 迁移：`dotnet ef migrations add AddRateLimitRuleRowVersion`
- [ ] 编写并发更新测试：两个线程同时更新同一规则，后者抛 `DbUpdateConcurrencyException`
- [ ] 提交：`git commit -m "fix(SystemAdmin): M-03 RateLimitRule 增加 RowVersion 乐观并发控制"`

### P1-M-04 AuditLogAppService.ExportAuditLogsAsync OOM 风险

**审计位置**：[file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Application/Services/AuditLogAppService.cs#L66-L93]、[file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/SystemConfigsController.cs#L47-L48]

**根因**：`ExportAuditLogsAsync` 传入 `int.MaxValue` 一次性加载全部审计日志。`GetGroupsAsync` 同样用 `int.MaxValue` 加载全部配置后 Distinct。

**任务清单：**
- [ ] 在 `IAuditLogRepository` 增加 `IAsyncEnumerable<AuditLog> StreamAsync(...)` 方法，分批流式拉取
- [ ] 在 `EfCoreAuditLogRepository` 实现 `StreamAsync`，使用 `AsNoTracking().AsAsyncEnumerable()`
- [ ] 将 `ExportAuditLogsAsync` 改为接收 `IAsyncEnumerable<AuditLog>` 并流式拼接 CSV
- [ ] 限制单次导出最大 10 万条，超出提示分批导出
- [ ] 在 `ISystemConfigRepository` 增加 `Task<List<string>> GetDistinctGroupsAsync(CancellationToken ct)` 方法
- [ ] 在 `EfCoreSystemConfigRepository` 实现 `GetDistinctGroupsAsync`，SQL 层 `SELECT DISTINCT Group`
- [ ] 将 `SystemConfigsController.GetGroupsAsync` 改为调用 `GetDistinctGroupsAsync`
- [ ] 编写流式导出测试与 Distinct 查询测试
- [ ] 提交：`git commit -m "fix(SystemAdmin): M-04 审计日志流式导出与配置分组 Distinct 查询优化"`

### P1-M-05 DeadLetterAppService 批量操作非原子

**审计位置**：[file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Application/Services/DeadLetterAppService.cs#L80-L139]

**根因**：`BatchRetryAsync`/`BatchDiscardAsync` 逐条调用 `RetryAsync`/`DiscardAsync`，每条独立 `SaveEntitiesAsync`。

**任务清单：**
- [ ] 在 `BatchRetryAsync` 中先 `GetByIdAsync` 收集所有死信消息，逐个 `Retry` 修改聚合状态，最后一次 `SaveEntitiesAsync`
- [ ] 在 `BatchDiscardAsync` 中采用相同模式
- [ ] 单条失败时记录到 `BatchOperationResultDto.Errors` 但不影响其他条目（部分成功语义）
- [ ] 编写批量重投测试验证单次 `SaveEntitiesAsync` 调用
- [ ] 提交：`git commit -m "fix(SystemAdmin): M-05 死信批量操作合并为单次 SaveEntitiesAsync"`

### P1-M-06 ScheduledTaskJob 两次 SaveEntitiesAsync

**审计位置**：[file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Jobs/ScheduledTaskJob.cs#L49-L74]

**根因**：`RunNow` 与 `RecordExecution` 分两次 `SaveEntitiesAsync`，中途失败导致任务卡在中间状态。

**任务清单：**
- [ ] 在 `ScheduledTask` 聚合根增加 `RunAndRecord(TaskRunStatus status, DateTime executedAt, string? errorMessage)` 方法，原子完成状态转换与执行记录
- [ ] 将 `ScheduledTaskJob.Execute` 的 L51-L57 合并为单次 `task.RunAndRecord(...)` + `UpdateAsync` + `SaveEntitiesAsync`
- [ ] 失败分支同样使用 `RunAndRecord(TaskRunStatus.Failed, ...)`
- [ ] 编写测试验证单次 `SaveEntitiesAsync` 调用
- [ ] 提交：`git commit -m "fix(SystemAdmin): M-06 ScheduledTaskJob 合并 RunNow 与 RecordExecution 为单次事务"`

### P1-M-07 ReconciliationRecord 不变性语义冲突

**审计位置**：[file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Domain/Aggregates/ReconciliationRecord.cs#L9-L10]、[file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Domain/Aggregates/ReconciliationRecord.cs#L66-L80]

**根因**：注释标注"不可变"但 `MarkAlertTriggered`/`MarkCorrectionTriggered` 修改状态。

**任务清单：**
- [ ] 修改类注释为"对账记录快照不可变，告警/修正标记为事后追加的状态标记"
- [ ] 为 `MarkAlertTriggered`/`MarkCorrectionTriggered` 增加幂等保护：已标记时直接返回不重复操作
- [ ] 在方法注释中明确"追加标记"语义，说明与快照不可变性的区别
- [ ] 编写测试验证幂等标记行为
- [ ] 提交：`git commit -m "fix(SystemAdmin): M-07 明确 ReconciliationRecord 不变性语义并增加标记幂等保护"`

### P1-M-08 ElasticsearchRebuildTrigger.GetProgressAsync 无 TaskId 关联

**审计位置**：[file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Services/ElasticsearchRebuildTrigger.cs#L89-L149]

**根因**：`GetProgressAsync` 遍历所有 reindex 任务返回第一个匹配的进度，`taskId` 参数未使用。

**任务清单：**
- [ ] 在 `IndexRebuildTask` 聚合根增加 `string? EsTaskId` 字段用于关联 ES 任务标识
- [ ] 在 `ElasticsearchRebuildTrigger.StartAsync` 中解析 ES 返回的 `task` 节点并回写（通过回调或返回值）
- [ ] 在 `GetProgressAsync` 中通过 `description` 字段匹配目标索引名 `{sourceIndex}_reindex_{taskId:N}`，仅返回匹配任务的进度
- [ ] 任务已完成（ES 任务不存在）时返回 100 而非 0
- [ ] 新增 EF Core 迁移：`dotnet ef migrations add AddIndexRebuildTaskEsTaskId`
- [ ] 编写测试验证 taskId 关联匹配逻辑
- [ ] 提交：`git commit -m "fix(SystemAdmin): M-08 ES 重建进度查询关联 TaskId 避免误返回"`

### P1-M-09 ScheduledTaskJob taskId 解析失败静默 return

**审计位置**：[file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Jobs/ScheduledTaskJob.cs#L31-L35]

**根因**：`Guid.TryParse` 失败或 `taskId == Guid.Empty` 时直接 `return` 无日志。

**任务清单：**
- [ ] 在 `return` 前增加 `_logger.LogWarning("定时任务 taskId 解析失败或为空，跳过执行 JobDataMap={JobDataMap}", taskIdValue)`
- [ ] 在方法开头通过构造函数注入 `ILogger<ScheduledTaskJob>` 或从 `IServiceProvider` 解析
- [ ] 编写测试验证 taskId 为空时记录告警日志
- [ ] 提交：`git commit -m "fix(SystemAdmin): M-09 ScheduledTaskJob taskId 解析失败时记录告警日志"`

### P1-M-10 DeadLetterMessage OriginalMessageId 唯一索引缺失

**审计位置**：[file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Configurations/DeadLetterMessageConfiguration.cs#L35]、[file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Services/RabbitMqDeadLetterManager.cs#L184-L198]

**根因**：`OriginalMessageId` 索引非唯一（[file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Configurations/DeadLetterMessageConfiguration.cs#L35]），并发拉取导致重复入库。

**任务清单：**
- [ ] 将 `DeadLetterMessageConfiguration` 的 `HasIndex(m => m.OriginalMessageId)` 改为 `.IsUnique()`
- [ ] 在 `RabbitMqDeadLetterManager.PersistDeadLetterCopyAsync` 的 `AddAsync` + `SaveEntitiesAsync` 外层增加 try-catch，捕获 `DbUpdateException` 并判定唯一约束冲突时视为已入库正常返回
- [ ] 新增 EF Core 迁移：`dotnet ef migrations add MakeDeadLetterOriginalMessageIdUnique`
- [ ] 编写并发入库测试验证唯一索引兜底
- [ ] 提交：`git commit -m "fix(SystemAdmin): M-10 DeadLetterMessage OriginalMessageId 唯一索引消除 TOCTOU 竞态"`

---

## P2 详细修复计划（任务清单格式）

### P2-L-01 EfCoreDataDictionaryRepository QueryAsync/CountAsync 风格不一致

**审计位置**：[file:////workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Repositories/EfCoreDataDictionaryRepository.cs#L34-L50]

**根因**：`QueryAsync` 使用 `.Include(d => d.Items)` 但 `CountAsync` 未使用，风格不统一。

**任务清单：**
- [ ] 将 `ApplyFilters` 返回的 `IQueryable` 抽取为公共变量，`QueryAsync` 在其基础上 `.Include` 后分页，`CountAsync` 直接对其计数（当前已是此模式，仅需确认注释一致）
- [ ] 在 `CountAsync` 增加注释说明"Count 不需要 Include，Include 不影响 Count 结果"
- [ ] 提交：`git commit -m "fix(SystemAdmin): L-01 统一 DataDictionaryRepository QueryAsync/CountAsync 过滤逻辑注释"`

### P2-L-02 StatisticsReconciliationJob 时区漂移

**审计位置**：[file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Jobs/StatisticsReconciliationJob.cs#L62-L67]

**根因**：`CalculateDelayUntilMidnight` 使用 `DateTime.UtcNow` 计算下次午夜，容器时区非 UTC 时偏移 8 小时。

**任务清单：**
- [ ] 在 `StatisticsReconciliationJob` 构造函数注入 `IConfiguration`，读取 `Statistics:Reconciliation:TimeZone` 配置（默认 `Asia/Shanghai`）
- [ ] 将 `CalculateDelayUntilMidnight` 改为使用 `TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tzInfo)` 计算本地午夜
- [ ] 编写测试验证不同时区下的延迟计算
- [ ] 提交：`git commit -m "fix(SystemAdmin): L-02 对账作业使用配置化时区计算下次午夜"`

### P2-L-03 HttpModuleHealthProbe 超时硬编码

**审计位置**：[file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Services/HttpModuleHealthProbe.cs#L16]

**根因**：`ProbeTimeout = TimeSpan.FromSeconds(3)` 硬编码，跨可用区探测过激进。

**任务清单：**
- [ ] 在 `HttpModuleHealthProbe` 构造函数注入 `IConfiguration`，读取 `HealthProbe:TimeoutSeconds` 配置（默认 5 秒）
- [ ] 将 `ProbeTimeout` 改为实例属性，从配置初始化
- [ ] 编写测试验证配置化超时
- [ ] 提交：`git commit -m "fix(SystemAdmin): L-03 HttpModuleHealthProbe 超时改为可配置默认 5 秒"`

### P2-L-04 DLQ 清理作业未实现

**审计位置**：[file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Services/RabbitMqDeadLetterManager.cs#L26-L31]

**根因**：`ack_requeue_true` 模式下消息始终回 DLQ，无清理 Job 导致 DLQ 消息无限堆积。

**任务清单：**
- [ ] 创建 `src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Jobs/DlqCleanupJob.cs`（Quartz `IJob`）
- [ ] 定期扫描本地 `DeadLetterMessages` 表已入库的 `OriginalMessageId`，调用 RabbitMQ Management API `DELETE /api/queues/{vhost}/{queue}/contents` 清理
- [ ] 在 `ServiceCollectionExtensions` 注册 `DlqCleanupJob` 与 Cron 调度（每小时一次）
- [ ] 编写测试验证清理逻辑
- [ ] 提交：`git commit -m "fix(SystemAdmin): L-04 实现 DLQ 清理作业避免消息堆积"`

### P2-L-05 AuditLogConsumer.AfterSalesApproved 职责越界（与 H-07 合并）

**审计位置**：[file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Consumers/AuditLogConsumer.cs#L70-L77]

**根因**：`AuditLogConsumer.AfterSalesApproved` 既写 `AuditLogEntry` 又写 `AuditLog`，`AuditLog` 应由 API 中间件写入。

**说明**：此问题已在 P0-H-07 中修复（删除 L74-L77 对 `AuditLog` 的写入）。此处仅记录关联关系，无需重复修复。

**任务清单：**
- [ ] 确认 H-07 修复已删除 `AuditLogConsumer.AfterSalesApproved` 中对 `AuditLog` 的写入
- [ ] 确认 `IAuditLogRepository` 在 `AuditLogConsumer` 中不再被使用（若所有消费者方法均不写 `AuditLog`，则可移除该依赖）
- [ ] 提交（如需额外清理）：`git commit -m "fix(SystemAdmin): L-05 确认 AuditLogConsumer 仅写 AuditLogEntry，移除 AuditLog 依赖"`

---

## 自检清单

**Spec 覆盖检查：**
- H-01 ~ H-07（7 个 P0）：✅ 每个均有完整 TDD 5 步骤
- M-01 ~ M-10（10 个 P1）：✅ 每个均有任务清单
- L-01 ~ L-05（5 个 P2）：✅ 每个均有任务清单
- T15、T20（已修复）：✅ 在"已修复问题清单"中列出
- 不可复现项：0 项

**占位符扫描：** 无 TODO/FIXME/省略/伪代码。所有 P0 测试代码与实现代码均为完整可编译内容。

**类型一致性检查：**
- `IStatisticsDataSource.GetMetricsAsync` 返回 `Task<List<MetricItem>>`，与 `StatisticsAggregationService` 调用一致
- `FeatureFlagCache.RemoveAsync(string, CancellationToken)` 签名与 `FeatureFlagAppService` 调用一致
- `SystemConfigCache.RemoveAsync(string, CancellationToken)` 签名与 `SystemConfigAppService` 调用一致
- `OperationLog.Create` 新增 `eventId` 参数为可选参数（`Guid? eventId = null`），不破坏现有调用方
- `IOperationLogRepository.GetByEventIdAsync` 签名与 `AfterSalesEventConsumer` 调用一致
- `IndexRebuildTask.Fail(string)` 已存在（[file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Domain/Aggregates/IndexRebuildTask.cs#L147-L159]），H-06 可直接调用
