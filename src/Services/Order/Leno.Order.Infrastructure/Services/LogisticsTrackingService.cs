using System.Text.Json;
using Leno.Order.Application.DTOs;
using Leno.Order.Application.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.Order.Infrastructure.Services;

/// <summary>
/// 物流轨迹查询服务实现，通过 HttpClient 调用第三方物流轨迹查询 API。
/// 查询失败或异常时返回空轨迹节点列表，不再返回占位文本。
/// </summary>
public sealed class LogisticsTrackingService : ILogisticsTrackingService
{
    private const string ApiKeyName = "API-Key";
    private const string DefaultApiUrl = "https://api.kdniao.com/Ebusiness/EbusinessOrderHandle.aspx";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly LogisticsApiOptions _options;
    private readonly ILogger<LogisticsTrackingService> _logger;

    public LogisticsTrackingService(
        HttpClient httpClient,
        IOptions<LogisticsApiOptions> options,
        ILogger<LogisticsTrackingService> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<LogisticsTrackingDto> GetTrackingAsync(string logisticsNo, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(logisticsNo))
        {
            return new LogisticsTrackingDto { LogisticsNo = logisticsNo, Nodes = new List<LogisticsTrackingNode>() };
        }

        try
        {
            var apiUrl = string.IsNullOrEmpty(_options.ApiUrl) ? DefaultApiUrl : _options.ApiUrl;
            var requestUrl = $"{apiUrl}?LogisticCode={Uri.EscapeDataString(logisticsNo)}";

            using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            if (!string.IsNullOrEmpty(_options.AppKey))
            {
                request.Headers.TryAddWithoutValidation(ApiKeyName, _options.AppKey);
            }

            using var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("物流查询失败 LogisticsNo={LogisticsNo} Status={Status}", logisticsNo, response.StatusCode);
                return new LogisticsTrackingDto { LogisticsNo = logisticsNo, Nodes = new List<LogisticsTrackingNode>() };
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var apiResponse = JsonSerializer.Deserialize<LogisticsApiResponse>(json, JsonOptions);

            var nodes = new List<LogisticsTrackingNode>();
            if (apiResponse?.Traces is not null)
            {
                foreach (var trace in apiResponse.Traces)
                {
                    nodes.Add(new LogisticsTrackingNode
                    {
                        Description = trace.AcceptStation ?? string.Empty,
                        Location = trace.Location ?? string.Empty,
                        OccurredAt = trace.AcceptTime ?? DateTime.UtcNow
                    });
                }
            }

            return new LogisticsTrackingDto
            {
                LogisticsNo = logisticsNo,
                Nodes = nodes
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "物流查询异常 LogisticsNo={LogisticsNo}", logisticsNo);
            return new LogisticsTrackingDto { LogisticsNo = logisticsNo, Nodes = new List<LogisticsTrackingNode>() };
        }
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
