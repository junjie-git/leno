using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Services;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SystemAdmin.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.SystemAdmin.Infrastructure.Services;

/// <summary>
/// 基于 HTTP 的 Alertmanager 客户端默认实现。
/// 调用 Alertmanager v2 API：
///   GET    /api/v2/alerts
///   GET    /api/v2/alerts/{id}
///   POST   /api/v2/alerts/{id}/acknowledge （注：Alertmanager v2 原生不支持单告警 acknowledge，
///          此实现通过创建静默规则等效实现 acknowledge 语义；如部署版本支持可改用原生端点）
///   POST   /api/v2/silences
///   GET    /api/v2/silences
///   DELETE /api/v2/silences/{id}
/// 当 <see cref="AlertmanagerOptions.Enabled"/> 为 false 或 <see cref="AlertmanagerOptions.BaseAddress"/> 为空时，
/// 客户端返回空结果（功能降级），便于无 Alertmanager 环境下启动与测试。
/// </summary>
public sealed class HttpAlertmanagerClient : IAlertmanagerClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly IOptionsMonitor<AlertmanagerOptions> _options;
    private readonly ILogger<HttpAlertmanagerClient> _logger;

    public HttpAlertmanagerClient(
        HttpClient httpClient,
        IOptionsMonitor<AlertmanagerOptions> options,
        ILogger<HttpAlertmanagerClient> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _httpClient = httpClient;
        _options = options;
        _logger = logger;

        ApplyHttpClientConfiguration();
    }

    /// <inheritdoc />
    public async Task<AlertQueryResult> GetAlertsAsync(AlertQueryFilter filter, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        if (!IsEnabled())
        {
            return new AlertQueryResult { Items = new List<Alert>(), Total = 0 };
        }

        try
        {
            var query = BuildAlertsQuery(filter);
            var response = await SendAsync(HttpMethod.Get, $"/api/v2/alerts{query}", ct: ct);
            if (!response.IsSuccessStatusCode)
            {
                LogError(response, "查询告警列表");
                return new AlertQueryResult { Items = new List<Alert>(), Total = 0 };
            }

            var alertsPayload = await response.Content.ReadFromJsonAsync<List<AlertmanagerAlertPayload>>(JsonOptions, ct)
                ?? new List<AlertmanagerAlertPayload>();

            var filtered = alertsPayload
                .Where(a => MatchesFilter(a, filter))
                .OrderByDescending(a => a.StartsAt)
                .ToList();

            var total = filtered.Count;
            var page = filter.Page < 1 ? 1 : filter.Page;
            var pageSize = filter.PageSize < 1 ? 20 : filter.PageSize;
            var paged = filtered
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(ToDomain)
                .ToList();

            return new AlertQueryResult { Items = paged, Total = total };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查询告警列表失败");
            return new AlertQueryResult { Items = new List<Alert>(), Total = 0 };
        }
    }

    /// <inheritdoc />
    public async Task<Alert?> GetAlertAsync(Guid alertId, CancellationToken ct = default)
    {
        if (alertId == Guid.Empty)
        {
            return null;
        }

        if (!IsEnabled())
        {
            return null;
        }

        try
        {
            // Alertmanager v2 没有"按 ID 获取单个告警"端点，使用列表查询 + fingerprint 过滤
            var response = await SendAsync(HttpMethod.Get, "/api/v2/alerts", ct: ct);
            if (!response.IsSuccessStatusCode)
            {
                LogError(response, "查询告警详情");
                return null;
            }

            var alertsPayload = await response.Content.ReadFromJsonAsync<List<AlertmanagerAlertPayload>>(JsonOptions, ct)
                ?? new List<AlertmanagerAlertPayload>();

            var match = alertsPayload.FirstOrDefault(a =>
                string.Equals(a.Fingerprint, alertId.ToString("N"), StringComparison.OrdinalIgnoreCase)
                || string.Equals(a.Fingerprint, alertId.ToString(), StringComparison.OrdinalIgnoreCase));

            return match is null ? null : ToDomain(match);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查询告警详情失败 AlertId={AlertId}", alertId);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task AcknowledgeAlertAsync(Guid alertId, string operatorId, string? comment, CancellationToken ct = default)
    {
        if (alertId == Guid.Empty)
        {
            throw new ArgumentException("告警标识不可为空", nameof(alertId));
        }
        if (string.IsNullOrWhiteSpace(operatorId))
        {
            throw new ArgumentException("操作者标识不可为空", nameof(operatorId));
        }

        if (!IsEnabled())
        {
            _logger.LogWarning("Alertmanager 未启用，告警确认操作被跳过 AlertId={AlertId}", alertId);
            return;
        }

        try
        {
            // Alertmanager v2 无原生 acknowledge 端点，通过创建静默规则等效实现。
            // 静默匹配器按 alertname + fingerprint 精确匹配，持续 1 小时（默认），原因记录操作者与备注。
            var alert = await GetAlertAsync(alertId, ct);
            if (alert is null)
            {
                throw new InvalidOperationException($"告警 {alertId} 不存在");
            }

            var matchers = new List<object>
            {
                new { name = "alertname", value = alert.Name, isRegex = false }
            };
            if (alert.Labels.TryGetValue("fingerprint", out var fp) && !string.IsNullOrWhiteSpace(fp))
            {
                matchers.Add(new { name = "fingerprint", value = fp, isRegex = false });
            }

            var payload = new
            {
                matchers = matchers,
                startsAt = DateTime.UtcNow,
                endsAt = DateTime.UtcNow.AddHours(1),
                createdBy = operatorId,
                comment = string.IsNullOrWhiteSpace(comment)
                    ? $"acknowledge by {operatorId}"
                    : $"acknowledge by {operatorId}: {comment}"
            };

            var response = await SendAsync(HttpMethod.Post, "/api/v2/silences", payload, ct);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException($"Alertmanager 确认告警失败：{(int)response.StatusCode} {body}");
            }

            _logger.LogInformation(
                "告警 {AlertId} 已通过创建静默规则等效确认 OperatorId={OperatorId}",
                alertId, operatorId);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "确认告警失败 AlertId={AlertId}", alertId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<AlertSilence> CreateSilenceAsync(
        string matchersJson,
        string duration,
        string reason,
        string createdBy,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(matchersJson))
        {
            throw new ArgumentException("匹配器不可为空", nameof(matchersJson));
        }
        if (string.IsNullOrWhiteSpace(duration))
        {
            throw new ArgumentException("持续时长不可为空", nameof(duration));
        }
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("静默原因不可为空", nameof(reason));
        }
        if (string.IsNullOrWhiteSpace(createdBy))
        {
            throw new ArgumentException("创建人标识不可为空", nameof(createdBy));
        }

        var endsAt = ComputeEndsAt(duration);

        if (!IsEnabled())
        {
            // 降级：返回本地构建的静默规则（不真正写入 Alertmanager），便于无 Alertmanager 环境下流程跑通
            _logger.LogWarning("Alertmanager 未启用，静默规则仅在本地构建，未实际生效");
            return AlertSilence.Create(
                Guid.NewGuid(),
                matchersJson,
                duration,
                reason,
                DateTime.UtcNow,
                endsAt,
                createdBy,
                DateTime.UtcNow);
        }

        try
        {
            var payload = new
            {
                matchers = JsonSerializer.Deserialize<System.Text.Json.JsonElement>(matchersJson),
                startsAt = DateTime.UtcNow,
                endsAt = endsAt,
                createdBy = createdBy,
                comment = reason
            };

            var response = await SendAsync(HttpMethod.Post, "/api/v2/silences", payload, ct);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException($"Alertmanager 创建静默规则失败：{(int)response.StatusCode} {body}");
            }

            var created = await response.Content.ReadFromJsonAsync<AlertmanagerSilenceIdPayload>(JsonOptions, ct);
            var silenceId = created?.SilenceId ?? Guid.NewGuid();

            return AlertSilence.Create(
                silenceId,
                matchersJson,
                duration,
                reason,
                DateTime.UtcNow,
                endsAt,
                createdBy,
                DateTime.UtcNow);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建静默规则失败");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<AlertSilence>> GetSilencesAsync(CancellationToken ct = default)
    {
        if (!IsEnabled())
        {
            return new List<AlertSilence>();
        }

        try
        {
            var response = await SendAsync(HttpMethod.Get, "/api/v2/silences", ct: ct);
            if (!response.IsSuccessStatusCode)
            {
                LogError(response, "查询静默规则列表");
                return new List<AlertSilence>();
            }

            var silences = await response.Content.ReadFromJsonAsync<List<AlertmanagerSilencePayload>>(JsonOptions, ct)
                ?? new List<AlertmanagerSilencePayload>();

            return silences
                .OrderByDescending(s => s.StartsAt)
                .Select(ToDomain)
                .Where(s => s is not null)
                .Cast<AlertSilence>()
                .ToList();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查询静默规则列表失败");
            return new List<AlertSilence>();
        }
    }

    /// <inheritdoc />
    public async Task DeleteSilenceAsync(Guid silenceId, CancellationToken ct = default)
    {
        if (silenceId == Guid.Empty)
        {
            throw new ArgumentException("静默规则标识不可为空", nameof(silenceId));
        }

        if (!IsEnabled())
        {
            _logger.LogWarning("Alertmanager 未启用，删除静默规则操作被跳过 SilenceId={SilenceId}", silenceId);
            return;
        }

        try
        {
            var response = await SendAsync(HttpMethod.Delete, $"/api/v2/silences/{silenceId:D}", ct: ct);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException($"Alertmanager 删除静默规则失败：{(int)response.StatusCode} {body}");
            }

            _logger.LogInformation("静默规则已删除 SilenceId={SilenceId}", silenceId);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除静默规则失败 SilenceId={SilenceId}", silenceId);
            throw;
        }
    }

    private bool IsEnabled()
    {
        var opts = _options.CurrentValue;
        return opts.Enabled && !string.IsNullOrWhiteSpace(opts.BaseAddress);
    }

    private void ApplyHttpClientConfiguration()
    {
        var opts = _options.CurrentValue;
        if (!string.IsNullOrWhiteSpace(opts.BaseAddress))
        {
            _httpClient.BaseAddress = new Uri(opts.BaseAddress.TrimEnd('/'));
        }
        _httpClient.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds <= 0 ? 10 : opts.TimeoutSeconds);
        if (!string.IsNullOrWhiteSpace(opts.AuthToken))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", opts.AuthToken);
        }
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        object? body = null,
        CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }
        return await _httpClient.SendAsync(request, ct);
    }

    private static string BuildAlertsQuery(AlertQueryFilter filter)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(filter.Module))
        {
            parts.Add($"filter={Uri.EscapeDataString($"{{module=\"{filter.Module}\"}}")}");
        }
        if (filter.Severity.HasValue)
        {
            parts.Add($"filter={Uri.EscapeDataString($"{{severity=\"{ToSeverityString(filter.Severity.Value)}\"}}")}");
        }
        if (filter.Status.HasValue)
        {
            parts.Add($"active={filter.Status.Value == AlertStatus.Firing}");
            parts.Add($"silenced={filter.Status.Value == AlertStatus.Acknowledged}");
        }
        return parts.Count == 0 ? string.Empty : "?" + string.Join("&", parts);
    }

    private static bool MatchesFilter(AlertmanagerAlertPayload alert, AlertQueryFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.Module))
        {
            if (!alert.Labels.TryGetValue("module", out var module) ||
                !string.Equals(module, filter.Module, StringComparison.Ordinal))
            {
                return false;
            }
        }
        if (filter.Severity.HasValue)
        {
            if (!alert.Labels.TryGetValue("severity", out var severity) ||
                !string.Equals(severity, ToSeverityString(filter.Severity.Value), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }
        if (filter.Start.HasValue && alert.StartsAt < filter.Start.Value)
        {
            return false;
        }
        if (filter.End.HasValue && alert.StartsAt > filter.End.Value)
        {
            return false;
        }
        return true;
    }

    private static string ToSeverityString(AlertSeverity severity)
        => severity switch
        {
            AlertSeverity.Critical => "critical",
            AlertSeverity.Warning => "warning",
            AlertSeverity.Info => "info",
            _ => "info"
        };

    private static AlertSeverity ParseSeverity(string? severity)
        => severity?.ToLowerInvariant() switch
        {
            "critical" => AlertSeverity.Critical,
            "warning" => AlertSeverity.Warning,
            _ => AlertSeverity.Info
        };

    private static AlertStatus ParseStatus(AlertmanagerAlertPayload alert)
    {
        if (alert.Status?.State?.Equals("resolved", StringComparison.OrdinalIgnoreCase) == true)
        {
            return AlertStatus.Resolved;
        }
        if (alert.Status?.State?.Equals("suppressed", StringComparison.OrdinalIgnoreCase) == true
            || alert.SilencedBy is { Count: > 0 })
        {
            return AlertStatus.Acknowledged;
        }
        return AlertStatus.Firing;
    }

    private static Alert ToDomain(AlertmanagerAlertPayload payload)
    {
        var id = Guid.TryParse(payload.Fingerprint, out var parsed) ? parsed : Guid.NewGuid();
        var name = payload.Labels.TryGetValue("alertname", out var alertName) ? alertName : "Unknown";
        var module = payload.Labels.TryGetValue("module", out var m) ? m : "Unknown";
        var severity = ParseSeverity(payload.Labels.TryGetValue("severity", out var s) ? s : null);
        var status = ParseStatus(payload);
        var summary = payload.Annotations.TryGetValue("summary", out var sum) ? sum : null;
        var description = payload.Annotations.TryGetValue("description", out var desc) ? desc : null;
        var relatedMetric = payload.Annotations.TryGetValue("related_metric", out var rm) ? rm : null;
        var triggeredAt = payload.StartsAt == default ? DateTime.UtcNow : payload.StartsAt;
        var durationSeconds = payload.EndsAt > triggeredAt
            ? (long)(payload.EndsAt - triggeredAt).TotalSeconds
            : (long)(DateTime.UtcNow - triggeredAt).TotalSeconds;
        if (durationSeconds < 0)
        {
            durationSeconds = 0;
        }

        return Alert.Create(
            id,
            name,
            module,
            severity,
            status,
            new Dictionary<string, string>(payload.Labels, StringComparer.Ordinal),
            new Dictionary<string, string>(payload.Annotations, StringComparer.Ordinal),
            relatedMetric,
            summary,
            description,
            triggeredAt,
            durationSeconds);
    }

    private static AlertSilence? ToDomain(AlertmanagerSilencePayload payload)
    {
        if (payload.Id is null || !Guid.TryParse(payload.Id, out var silenceId))
        {
            return null;
        }
        var matchersJson = JsonSerializer.Serialize(
            payload.Matchers.Select(m => new { name = m.Name, value = m.Value, isRegex = m.IsRegex }),
            JsonOptions);
        var startsAt = payload.StartsAt == default ? DateTime.UtcNow : payload.StartsAt;
        var endsAt = payload.EndsAt == default ? startsAt.AddHours(1) : payload.EndsAt;
        var duration = endsAt > startsAt ? FormatDuration(endsAt - startsAt) : "1h";
        var reason = string.IsNullOrWhiteSpace(payload.Comment) ? "imported from Alertmanager" : payload.Comment;
        var createdBy = string.IsNullOrWhiteSpace(payload.CreatedBy) ? "unknown" : payload.CreatedBy;
        var createdAt = payload.UpdatedAt == default ? startsAt : payload.UpdatedAt;

        return AlertSilence.Create(
            silenceId,
            matchersJson,
            duration,
            reason,
            startsAt,
            endsAt,
            createdBy,
            createdAt);
    }

    private static string FormatDuration(TimeSpan span)
    {
        if (span.TotalDays >= 1 && span.TotalDays % 1 == 0)
        {
            return $"{(int)span.TotalDays}d";
        }
        if (span.TotalHours >= 1 && span.TotalHours % 1 == 0)
        {
            return $"{(int)span.TotalHours}h";
        }
        if (span.TotalMinutes >= 1)
        {
            return $"{(int)span.TotalMinutes}m";
        }
        return "1h";
    }

    private static DateTime ComputeEndsAt(string duration)
    {
        var now = DateTime.UtcNow;
        if (string.IsNullOrWhiteSpace(duration))
        {
            return now.AddHours(1);
        }

        var d = duration.Trim().ToLowerInvariant();
        if (d.Length < 2 || !int.TryParse(d[..^1], out var value) || value <= 0)
        {
            return now.AddHours(1);
        }

        return d[^1] switch
        {
            'm' => now.AddMinutes(value),
            'h' => now.AddHours(value),
            'd' => now.AddDays(value),
            _ => now.AddHours(1)
        };
    }

    private void LogError(HttpResponseMessage response, string operation)
    {
        _logger.LogWarning("{Operation}返回非成功状态码：{StatusCode}", operation, (int)response.StatusCode);
    }

    // ============================================================
    // Alertmanager API 响应负载（与 Alertmanager v2 API 一致）
    // ============================================================

    private sealed class AlertmanagerAlertPayload
    {
        [JsonPropertyName("fingerprint")]
        public string? Fingerprint { get; set; }

        [JsonPropertyName("startsAt")]
        public DateTime StartsAt { get; set; }

        [JsonPropertyName("endsAt")]
        public DateTime EndsAt { get; set; }

        [JsonPropertyName("labels")]
        public Dictionary<string, string> Labels { get; set; } = new();

        [JsonPropertyName("annotations")]
        public Dictionary<string, string> Annotations { get; set; } = new();

        [JsonPropertyName("status")]
        public AlertmanagerStatusPayload? Status { get; set; }

        [JsonPropertyName("silencedBy")]
        public List<string>? SilencedBy { get; set; }
    }

    private sealed class AlertmanagerStatusPayload
    {
        [JsonPropertyName("state")]
        public string? State { get; set; }
    }

    private sealed class AlertmanagerSilencePayload
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("matchers")]
        public List<AlertmanagerMatcherPayload> Matchers { get; set; } = new();

        [JsonPropertyName("startsAt")]
        public DateTime StartsAt { get; set; }

        [JsonPropertyName("endsAt")]
        public DateTime EndsAt { get; set; }

        [JsonPropertyName("createdBy")]
        public string? CreatedBy { get; set; }

        [JsonPropertyName("comment")]
        public string? Comment { get; set; }

        [JsonPropertyName("updatedAt")]
        public DateTime UpdatedAt { get; set; }
    }

    private sealed class AlertmanagerMatcherPayload
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        public string Value { get; set; } = string.Empty;

        [JsonPropertyName("isRegex")]
        public bool IsRegex { get; set; }
    }

    private sealed class AlertmanagerSilenceIdPayload
    {
        [JsonPropertyName("silenceID")]
        public Guid? SilenceId { get; set; }
    }
}
