using System.Text.Json;
using Leno.Infrastructure.AntiCorruption;
using Leno.Order.Domain.Repositories;
using Leno.Order.Domain.Services;
using Leno.Order.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Leno.Order.Infrastructure.Services;

/// <summary>
/// 物流轨迹查询服务实现，实现领域层 <see cref="ILogisticsTrackingService"/>。
/// 通过 HttpClient 调用第三方物流轨迹查询 API，结果缓存至 Redis（TTL 1 小时）。
/// 继承 <see cref="AntiCorruptionBase"/>：第三方 API 失败时降级返回缓存或空轨迹（不抛异常），仅当缓存读取本身异常时由基类统一捕获埋点。
/// P1-T25：API 失败时上报 <see cref="AntiCorruptionMetrics"/> 指标，连续失败超阈值（5 次）切换降级模式
/// （<see cref="IsDegraded"/>=true + 熔断器 Open 状态），恢复后自动复位。避免第三方持续故障时运维无感知。
/// 缓存 key 格式：logistics:trace:{logisticsNo}:{companyCode}。
/// </summary>
public sealed class LogisticsTrackingService : AntiCorruptionBase, ILogisticsTrackingService
{
    private const string ApiKeyName = "API-Key";
    private const string DefaultApiUrl = "https://api.kdniao.com/Ebusiness/EbusinessOrderHandle.aspx";
    private const string CacheKeyPrefix = "logistics:trace:";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);

    /// <summary>连续失败次数达到此阈值时切换为降级模式（P1-T25）。</summary>
    private const int DegradationThreshold = 5;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly LogisticsApiOptions _options;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<LogisticsTrackingService> _logger;

    /// <summary>连续失败计数（线程安全，经 <see cref="Interlocked"/> 操作）。</summary>
    private int _consecutiveFailures;

    /// <summary>
    /// 当前是否处于降级模式（P1-T25）。
    /// 连续失败 ≥ <see cref="DegradationThreshold"/> 时为 true，API 恢复后自动复位为 false。
    /// 经 <see cref="AntiCorruptionMetrics.UpdateCircuitOpenState"/> 同步至 Prometheus gauge 供运维监控。
    /// </summary>
    public bool IsDegraded => Volatile.Read(ref _consecutiveFailures) >= DegradationThreshold;

    protected override string ServiceName => "logistics";

    public LogisticsTrackingService(
        HttpClient httpClient,
        IOptions<LogisticsApiOptions> options,
        IConnectionMultiplexer redis,
        ILogger<LogisticsTrackingService> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(redis);
        ArgumentNullException.ThrowIfNull(logger);
        _httpClient = httpClient;
        _options = options.Value;
        _redis = redis;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<LogisticsTraceResult> QueryTraceAsync(
        string logisticsNo, string companyCode, CancellationToken ct = default)
        => ExecuteAsync("query_trace", async token =>
        {
            if (string.IsNullOrWhiteSpace(logisticsNo))
            {
                return LogisticsTraceResult.Empty(logisticsNo ?? string.Empty, companyCode ?? string.Empty);
            }

            var cacheKey = $"{CacheKeyPrefix}{logisticsNo}:{companyCode}";

            // 第三方物流接口允许降级：API 失败或异常时返回缓存或空轨迹，不抛异常
            try
            {
                var apiUrl = string.IsNullOrEmpty(_options.ApiUrl) ? DefaultApiUrl : _options.ApiUrl;
                var requestUrl = $"{apiUrl}?LogisticCode={Uri.EscapeDataString(logisticsNo)}&ShipperCode={Uri.EscapeDataString(companyCode)}";

                using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
                if (!string.IsNullOrEmpty(_options.AppKey))
                {
                    request.Headers.TryAddWithoutValidation(ApiKeyName, _options.AppKey);
                }

                using var response = await _httpClient.SendAsync(request, token);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync(token);
                    var apiResponse = JsonSerializer.Deserialize<LogisticsApiResponse>(json, JsonOptions);

                    var nodes = new List<LogisticsTraceNode>();
                    if (apiResponse?.Traces is not null)
                    {
                        foreach (var trace in apiResponse.Traces)
                        {
                            nodes.Add(new LogisticsTraceNode(
                                trace.AcceptStation ?? string.Empty,
                                trace.AcceptTime ?? DateTime.UtcNow,
                                trace.Location ?? string.Empty));
                        }
                    }

                    var result = new LogisticsTraceResult(logisticsNo, companyCode, nodes, false);
                    await CacheResultAsync(cacheKey, result, token);
                    // P1-T25：API 恢复后复位降级状态与熔断器
                    ResetFailureState();
                    return result;
                }

                // P1-T25：非成功状态码上报失败指标
                _logger.LogWarning("物流查询失败 LogisticsNo={LogisticsNo} CompanyCode={CompanyCode} Status={Status}",
                    logisticsNo, companyCode, (int)response.StatusCode);
                RecordFailure();
            }
            catch (Exception ex)
            {
                // P1-T25：异常上报失败指标，持续失败触发降级
                _logger.LogWarning(ex, "物流查询异常 LogisticsNo={LogisticsNo} CompanyCode={CompanyCode}", logisticsNo, companyCode);
                RecordFailure();
            }

            // API 失败或异常时尝试从缓存获取
            var cached = await GetCachedResultAsync(cacheKey, token);
            if (cached is not null)
            {
                return new LogisticsTraceResult(logisticsNo, companyCode,
                    cached.Nodes.Select(n => new LogisticsTraceNode(n.Description, n.OccurredAt, n.Location)), true);
            }

            return LogisticsTraceResult.Empty(logisticsNo, companyCode);
        }, ct);

    /// <summary>
    /// P1-T25：记录一次连续失败，递增计数器并上报指标。
    /// 当连续失败次数达到 <see cref="DegradationThreshold"/> 时切换为降级模式（熔断器 Open）。
    /// </summary>
    private void RecordFailure()
    {
        AntiCorruptionMetrics.RecordFailure(ServiceName, "query_trace");
        var current = Interlocked.Increment(ref _consecutiveFailures);
        if (current == DegradationThreshold)
        {
            // 首次达到阈值时切换为降级模式，更新熔断器 Open 状态并记录严重告警
            AntiCorruptionMetrics.UpdateCircuitOpenState(ServiceName, isOpen: true);
            _logger.LogCritical(
                "物流轨迹服务进入降级模式：连续失败 {FailureCount} 次达阈值 {Threshold}，后续查询将降级返回缓存或空轨迹",
                current, DegradationThreshold);
        }
    }

    /// <summary>
    /// P1-T25：API 恢复后复位连续失败计数与降级状态。
    /// 仅在之前处于降级模式时更新熔断器状态并记录恢复日志，避免每次成功请求都写日志。
    /// </summary>
    private void ResetFailureState()
    {
        var previous = Interlocked.Exchange(ref _consecutiveFailures, 0);
        if (previous >= DegradationThreshold)
        {
            AntiCorruptionMetrics.UpdateCircuitOpenState(ServiceName, isOpen: false);
            _logger.LogInformation(
                "物流轨迹服务退出降级模式：连续失败计数从 {PreviousCount} 复位，API 已恢复",
                previous);
        }
    }

    /// <summary>
    /// 将物流轨迹结果缓存到 Redis。
    /// </summary>
    private async Task CacheResultAsync(string cacheKey, LogisticsTraceResult result, CancellationToken ct)
    {
        try
        {
            var db = _redis.GetDatabase();
            var cacheData = new CacheEntry
            {
                LogisticsNo = result.LogisticsNo,
                CompanyCode = result.CompanyCode,
                Nodes = result.Nodes.Select(n => new CacheTraceNode
                {
                    Description = n.Description,
                    OccurredAt = n.OccurredAt,
                    Location = n.Location
                }).ToList()
            };
            var json = JsonSerializer.Serialize(cacheData, JsonOptions);
            await db.StringSetAsync(cacheKey, json, CacheTtl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "物流轨迹缓存写入失败 Key={CacheKey}", cacheKey);
        }
    }

    /// <summary>
    /// 从 Redis 获取缓存的物流轨迹结果。
    /// </summary>
    private async Task<CacheEntry?> GetCachedResultAsync(string cacheKey, CancellationToken ct)
    {
        try
        {
            var db = _redis.GetDatabase();
            var json = await db.StringGetAsync(cacheKey);
            if (json.IsNullOrEmpty)
            {
                return null;
            }

            return JsonSerializer.Deserialize<CacheEntry>(json.ToString(), JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "物流轨迹缓存读取失败 Key={CacheKey}", cacheKey);
            return null;
        }
    }

    private sealed class CacheEntry
    {
        public string LogisticsNo { get; set; } = string.Empty;
        public string CompanyCode { get; set; } = string.Empty;
        public List<CacheTraceNode> Nodes { get; set; } = new();
    }

    private sealed class CacheTraceNode
    {
        public string Description { get; set; } = string.Empty;
        public DateTime OccurredAt { get; set; }
        public string Location { get; set; } = string.Empty;
    }

    private sealed class LogisticsApiResponse
    {
        public List<LogisticsTrace>? Traces { get; set; }
    }

    private sealed class LogisticsTrace
    {
        public string? AcceptStation { get; set; }

        public string? Location { get; set; }

        public DateTime? AcceptTime { get; set; }
    }
}
