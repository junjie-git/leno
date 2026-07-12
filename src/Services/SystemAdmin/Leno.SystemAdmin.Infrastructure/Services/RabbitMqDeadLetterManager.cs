using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Leno.SystemAdmin.Infrastructure.Services;

/// <summary>
/// RabbitMQ 死信队列管理器实现，通过 RabbitMQ Management HTTP API 拉取死信消息。
/// 默认连接 RabbitMQ Management API（端口 15672），使用 Basic Auth 认证。
/// 配置节：<c>RabbitMQ:ManagementApi</c>，包含 Host、Username、Password、VHost。
/// </summary>
public sealed class RabbitMqDeadLetterManager : IDeadLetterQueueManager
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RabbitMqDeadLetterManager> _logger;

    private const string ManagementApiConfigKey = "RabbitMQ:ManagementApi";

    public RabbitMqDeadLetterManager(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<RabbitMqDeadLetterManager> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;

        ConfigureHttpClient();
    }

    /// <inheritdoc />
    public async Task<List<DeadLetterMessage>> FetchAsync(string? sourceContext, int page, int pageSize, CancellationToken ct = default)
    {
        var baseUrl = GetManagementApiBaseUrl();
        var vhost = GetVHost();
        var dlqName = GetDeadLetterQueueName(sourceContext);

        var url = $"{baseUrl}/api/queues/{Uri.EscapeDataString(vhost)}/{Uri.EscapeDataString(dlqName)}/get";

        var requestBody = new
        {
            count = pageSize,
            ackmode = "ack_requeue_false",
            encoding = "auto",
            truncate = 50000
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        _logger.LogDebug("从 RabbitMQ 拉取死信：URL={Url}, Count={Count}", url, pageSize);

        var response = await _httpClient.PostAsync(url, content, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("RabbitMQ Management API 返回错误：StatusCode={StatusCode}, Body={Body}",
                (int)response.StatusCode, errorBody);
            return [];
        }

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        var messages = ParseDeadLetterMessages(responseJson, sourceContext);

        // 分页处理
        var skip = (page - 1) * pageSize;
        return messages.Skip(skip).Take(pageSize).ToList();
    }

    /// <inheritdoc />
    public async Task<int> CountAsync(string? sourceContext, CancellationToken ct = default)
    {
        var baseUrl = GetManagementApiBaseUrl();
        var vhost = GetVHost();
        var dlqName = GetDeadLetterQueueName(sourceContext);

        var url = $"{baseUrl}/api/queues/{Uri.EscapeDataString(vhost)}/{Uri.EscapeDataString(dlqName)}";

        _logger.LogDebug("查询死信队列消息数量：URL={Url}", url);

        var response = await _httpClient.GetAsync(url, ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("RabbitMQ Management API 查询队列信息失败：StatusCode={StatusCode}",
                (int)response.StatusCode);
            return 0;
        }

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(responseJson);

        if (doc.RootElement.TryGetProperty("messages_ready", out var messagesReady))
        {
            return messagesReady.GetInt32();
        }

        return 0;
    }

    /// <inheritdoc />
    public async Task RepublishAsync(Guid messageId, CancellationToken ct = default)
    {
        // RabbitMQ 死信队列重投需要将消息从 DLQ 重新发布到原队列
        // 通过 Management API 的 shovel 或直接 publish 实现
        // 此处为简化实现，记录日志表示操作意图
        _logger.LogWarning(
            "RabbitMqDeadLetterManager.RepublishAsync 通过 Management API 重投需要额外配置 shovel 或直接 publish 到原队列。" +
            "MessageId={MessageId}。生产环境需实现完整的重投流水线。", messageId);

        // 实际生产环境应：
        // 1. 从 DLQ 获取消息详情
        // 2. 解析 x-death 头获取原始 exchange/routing-key
        // 3. 通过 Management API 的 /api/exchanges/{vhost}/{exchange}/publish 重新发布
        await Task.CompletedTask;
    }

    private void ConfigureHttpClient()
    {
        var section = _configuration.GetSection(ManagementApiConfigKey);
        var username = section["Username"] ?? "guest";
        var password = section["Password"] ?? "guest";

        var authBytes = Encoding.UTF8.GetBytes($"{username}:{password}");
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
    }

    private string GetManagementApiBaseUrl()
    {
        var section = _configuration.GetSection(ManagementApiConfigKey);
        var host = section["Host"] ?? "http://localhost:15672";
        return host.TrimEnd('/');
    }

    private string GetVHost()
    {
        var section = _configuration.GetSection(ManagementApiConfigKey);
        return section["VHost"] ?? "%2F";
    }

    private string GetDeadLetterQueueName(string? sourceContext)
    {
        if (!string.IsNullOrWhiteSpace(sourceContext))
        {
            return $"{sourceContext}.dlq";
        }

        return "dead-letter-queue";
    }

    private List<DeadLetterMessage> ParseDeadLetterMessages(string responseJson, string? sourceContext)
    {
        var messages = new List<DeadLetterMessage>();

        try
        {
            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Array)
            {
                return messages;
            }

            foreach (var item in root.EnumerateArray())
            {
                var messageId = Guid.NewGuid();
                var originalMessageId = string.Empty;
                var originalTopic = string.Empty;
                var payload = string.Empty;
                var headers = string.Empty;
                var errorReason = string.Empty;

                // 提取 payload
                if (item.TryGetProperty("payload", out var payloadElement))
                {
                    payload = payloadElement.GetRawText();
                }

                // 提取 properties.headers
                if (item.TryGetProperty("properties", out var props) &&
                    props.TryGetProperty("headers", out var headersElement))
                {
                    headers = headersElement.GetRawText();

                    // 从 x-death 提取错误原因
                    if (headersElement.TryGetProperty("x-death", out var xDeath) &&
                        xDeath.ValueKind == JsonValueKind.Array &&
                        xDeath.GetArrayLength() > 0)
                    {
                        var firstDeath = xDeath[0];
                        if (firstDeath.TryGetProperty("reason", out var reason))
                        {
                            errorReason = reason.GetString() ?? string.Empty;
                        }

                        if (firstDeath.TryGetProperty("queue", out var queue))
                        {
                            originalTopic = queue.GetString() ?? string.Empty;
                        }
                    }

                    // 提取 message_id
                    if (headersElement.TryGetProperty("message_id", out var msgId))
                    {
                        originalMessageId = msgId.GetString() ?? string.Empty;
                    }
                }

                // 提取 routing_key
                if (item.TryGetProperty("routing_key", out var routingKey))
                {
                    originalTopic = routingKey.GetString() ?? originalTopic;
                }

                var message = DeadLetterMessage.Create(
                    messageId,
                    originalMessageId,
                    sourceContext ?? "rabbitmq",
                    originalTopic,
                    payload,
                    headers,
                    errorReason);

                messages.Add(message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解析 RabbitMQ 死信消息响应失败");
        }

        return messages;
    }
}