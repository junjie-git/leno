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
/// 缓存 key 格式：logistics:trace:{logisticsNo}:{companyCode}。
/// </summary>
public sealed class LogisticsTrackingService : AntiCorruptionBase, ILogisticsTrackingService
{
    private const string ApiKeyName = "API-Key";
    private const string DefaultApiUrl = "https://api.kdniao.com/Ebusiness/EbusinessOrderHandle.aspx";
    private const string CacheKeyPrefix = "logistics:trace:";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly LogisticsApiOptions _options;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<LogisticsTrackingService> _logger;

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
                    return result;
                }

                _logger.LogWarning("物流查询失败 LogisticsNo={LogisticsNo} CompanyCode={CompanyCode} Status={Status}",
                    logisticsNo, companyCode, (int)response.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "物流查询异常 LogisticsNo={LogisticsNo} CompanyCode={CompanyCode}", logisticsNo, companyCode);
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
