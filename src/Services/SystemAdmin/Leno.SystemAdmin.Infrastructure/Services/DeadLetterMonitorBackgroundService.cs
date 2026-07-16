using System.Diagnostics.Metrics;
using Leno.SystemAdmin.Domain.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.SystemAdmin.Infrastructure.Services;

/// <summary>
/// 死信积压告警后台服务配置（T20）。
/// 通过 <c>appsettings.json</c> 的 <c>DeadLetterMonitor</c> 节绑定。
/// </summary>
public sealed class DeadLetterMonitorOptions
{
    /// <summary>扫描间隔，默认 5 分钟。</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>死信数量告警阈值，默认 10。超过即记录告警日志。</summary>
    public int AlertThreshold { get; set; } = 10;

    /// <summary>
    /// 待扫描的来源上下文列表。为空时仅扫描总死信数量（sourceContext=null）。
    /// 示例：<c>["OrderService", "PaymentService", "PromotionService"]</c>
    /// </summary>
    public List<string> SourceContexts { get; set; } = new();
}

/// <summary>
/// 死信积压告警后台服务（T20）。
/// 定期调用 <see cref="IDeadLetterQueueManager.CountAsync"/> 扫描死信队列积压数量，
/// 超过 <see cref="DeadLetterMonitorOptions.AlertThreshold"/> 时记录告警日志并更新 Prometheus 指标
/// <c>dead_letter_count{source_context}</c>，便于监控发现消费者故障/下游异常导致的死信堆积。
/// 复用 T15 的 <see cref="IDeadLetterQueueManager.CountAsync"/> 实现（DB 仓储或 RabbitMQ Management API）。
/// </summary>
/// <remarks>
/// 需在 SystemAdmin 表现层 Program.cs 注册：
/// <c>services.AddHostedService&lt;DeadLetterMonitorBackgroundService&gt;();</c>
/// 并可选绑定配置：<c>services.Configure&lt;DeadLetterMonitorOptions&gt;(configuration.GetSection("DeadLetterMonitor"));</c>
/// </remarks>
public sealed class DeadLetterMonitorBackgroundService : BackgroundService
{
    /// <summary>Meter 名称，OTel SDK 须通过 <c>AddMeter(DeadLetterMonitorBackgroundService.MeterName)</c> 订阅。</summary>
    public const string MeterName = "Leno.SystemAdmin.DeadLetter";

    private const string SourceContextLabel = "source_context";

    private static readonly Meter _meter = new(MeterName, "1.0.0");

    /// <summary>
    /// 死信积压数量 ObservableGauge，标签 <c>source_context</c>（total 或具体 BC 名）。
    /// 对应 Prometheus 指标 <c>dead_letter_count</c>。
    /// </summary>
    public static ObservableGauge<int> DeadLetterCountGauge { get; } =
        _meter.CreateObservableGauge<int>(
            "dead_letter_count",
            observeValues: () => _latestObservations ?? new List<Measurement<int>>(),
            unit: "messages",
            description: "死信队列积压消息数量（按 source_context 维度统计）");

    /// <summary>
    /// 最新一轮扫描的死信数量观测值，供 ObservableGauge 回调读取。
    /// 由 <see cref="RunScanCycleAsync"/> 更新。
    /// </summary>
    private static List<Measurement<int>> _latestObservations = new() { new(0, new KeyValuePair<string, object?>[] { new(SourceContextLabel, "total") }) };

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DeadLetterMonitorBackgroundService> _logger;
    private readonly DeadLetterMonitorOptions _options;

    public DeadLetterMonitorBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<DeadLetterMonitorBackgroundService> logger,
        IOptions<DeadLetterMonitorOptions> options)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options.Value;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("死信积压告警服务已启动，扫描间隔 {Interval}，告警阈值 {Threshold}",
            _options.Interval, _options.AlertThreshold);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_options.Interval, stoppingToken);
                await RunScanCycleAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "死信积压告警服务执行异常");
            }
        }

        _logger.LogInformation("死信积压告警服务已停止");
    }

    /// <summary>
    /// 执行一轮死信积压扫描：查询总死信数量与各 sourceContext 数量，
    /// 超阈值时记录告警日志，并更新 Prometheus ObservableGauge 观测值。
    /// </summary>
    /// <param name="ct">取消令牌。</param>
    public async Task RunScanCycleAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IDeadLetterQueueManager>();

        var observations = new List<Measurement<int>>();
        var total = await manager.CountAsync(sourceContext: null, ct);
        observations.Add(new Measurement<int>(total, new KeyValuePair<string, object?>[] { new(SourceContextLabel, "total") }));

        if (total > 0)
        {
            _logger.LogInformation("死信队列当前积压 {Count} 条", total);
        }

        if (total > _options.AlertThreshold)
        {
            _logger.LogWarning("死信积压告警：当前死信数量 {Count} 超过阈值 {Threshold}，请检查消费者故障或下游异常",
                total, _options.AlertThreshold);
        }

        // 扫描配置的 sourceContexts，提供按 BC 维度的细粒度观测与告警
        foreach (var sourceContext in _options.SourceContexts)
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(sourceContext))
            {
                continue;
            }

            var count = await manager.CountAsync(sourceContext, ct);
            observations.Add(new Measurement<int>(count, new KeyValuePair<string, object?>[] { new(SourceContextLabel, sourceContext) }));

            if (count > _options.AlertThreshold)
            {
                _logger.LogWarning("死信积压告警：来源 {SourceContext} 死信数量 {Count} 超过阈值 {Threshold}",
                    sourceContext, count, _options.AlertThreshold);
            }
        }

        // 更新 ObservableGauge 的最新观测值（线程安全替换）
        Interlocked.Exchange(ref _latestObservations, observations);
    }
}
