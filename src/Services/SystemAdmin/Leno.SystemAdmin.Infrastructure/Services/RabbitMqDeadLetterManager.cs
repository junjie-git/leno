using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Leno.Infrastructure.Abstractions;
using Leno.SharedKernel.Abstractions;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Domain.Services;
using Leno.SystemAdmin.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Leno.SystemAdmin.Infrastructure.Services;

/// <summary>
/// RabbitMQ 死信队列管理器实现，通过 RabbitMQ Management HTTP API 拉取死信消息。
/// 默认连接 RabbitMQ Management API（端口 15672），使用 Basic Auth 认证。
/// 配置节：<c>RabbitMQ:ManagementApi</c>，包含 Host、Username、Password、VHost。
/// </summary>
/// <remarks>
/// 重投策略（与 <see cref="DeadLetterQueueManager"/> 行为一致）：
/// 通过注入的 <see cref="IEventBus"/> 将死信记录中的原始 <c>IIntegrationEvent</c> 反序列化后重新发布到 MQ，
/// 重投成功后更新死信记录状态为 Retried。共用 <see cref="DeadLetterRepublishHelper"/>。
///
/// FetchAsync 拉取策略（保证不丢消息）：
/// 采用 <c>ackmode=ack_requeue_true</c> 拉取（消息不删除、回队），同时入库 DeadLetter 副本（按 OriginalMessageId 去重）。
/// 这样即使本地入库失败，消息仍保留在 DLQ，下次拉取仍能拿到，确保不丢失。
/// 代价是消息在 DLQ 中重复存在，需配合独立的 DLQ 清理后台任务（如 Quartz Job）在副本入库成功后从 DLQ 移除原消息；
/// 该后台清理 Job 不在本任务范围内，由后续任务实现。
/// "本地处理成功后才从 DLQ 移除" 的语义通过"入库副本成功 → 后台异步清理 DLQ 原消息"实现。
/// </remarks>
public sealed class RabbitMqDeadLetterManager : IDeadLetterQueueManager
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly IDeadLetterMessageRepository _repository;
    private readonly IEventBus _eventBus;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RabbitMqDeadLetterManager> _logger;

    private const string ManagementApiConfigKey = "RabbitMQ:ManagementApi";

    public RabbitMqDeadLetterManager(
        HttpClient httpClient,
        IConfiguration configuration,
        IDeadLetterMessageRepository repository,
        IEventBus eventBus,
        IUnitOfWork unitOfWork,
        ILogger<RabbitMqDeadLetterManager> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(eventBus);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(logger);
        _httpClient = httpClient;
        _configuration = configuration;
        _repository = repository;
        _eventBus = eventBus;
        _unitOfWork = unitOfWork;
        _logger = logger;

        ConfigureHttpClient();
    }

    /// <inheritdoc />
    public async Task<List<DeadLetterMessage>> FetchAsync(string? sourceContext, int page, int pageSize, CancellationToken ct = default)
    {
        // 拉取策略：ack_requeue_true 拉取（消息不删除、回队）+ 入库副本（按 OriginalMessageId 去重）。
        // 保证不丢消息：入库失败时消息仍在 DLQ，下次拉取仍能拿到。
        // DLQ 中原消息的清理需由独立后台任务在副本入库成功后执行（本任务不实现）。
        var baseUrl = GetManagementApiBaseUrl();
        var vhost = GetVHost();
        var dlqName = GetDeadLetterQueueName(sourceContext);

        var url = $"{baseUrl}/api/queues/{Uri.EscapeDataString(vhost)}/{Uri.EscapeDataString(dlqName)}/get";

        var requestBody = new
        {
            count = pageSize,
            ackmode = "ack_requeue_true",
            encoding = "auto",
            truncate = 50000
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        _logger.LogDebug("从 RabbitMQ 拉取死信（ack_requeue_true，不删除）：URL={Url}, Count={Count}", url, pageSize);

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

        // 入库副本：按 OriginalMessageId 去重，避免重复拉取导致重复入库
        foreach (var message in messages)
        {
            await PersistDeadLetterCopyAsync(message, ct);
        }

        // 分页处理（拉取后内存分页，与原实现保持一致）
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
        // 行为与 DeadLetterQueueManager.RepublishAsync 一致：通过 IEventBus 真正重投原始集成事件。
        var message = await _repository.GetByIdAsync(messageId, ct);
        if (message is null)
        {
            throw new InvalidOperationException($"死信消息 {messageId} 不存在");
        }

        // 幂等：已重投则跳过重复发布
        if (message.Status == DeadLetterStatus.Retried)
        {
            _logger.LogInformation("死信消息 {MessageId} 已重投，跳过重复重投", messageId);
            return;
        }

        if (message.Status == DeadLetterStatus.Discarded)
        {
            throw new InvalidOperationException($"死信消息 {messageId} 已丢弃，不可重投");
        }

        // 真正重投：反序列化原始集成事件并通过事件总线重新发布到 MQ
        await DeadLetterRepublishHelper.RepublishViaEventBusAsync(_eventBus, message, _logger, ct);

        // 重投成功后标记消息状态为 Retried（经发件箱投递领域事件）
        message.Retry("system");
        await _repository.UpdateAsync(message, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        _logger.LogInformation("死信消息 {MessageId} 已通过事件总线重投", messageId);
    }

    /// <summary>
    /// 入库死信副本：按 OriginalMessageId 去重，已存在则跳过。
    /// 入库失败抛异常，由调用方感知；因 ack_requeue_true 消息已回 DLQ，下次拉取仍能拿到，不丢失。
    /// 并发拉取时由 OriginalMessageId 唯一索引兜底：捕获 DbUpdateException 判定为唯一约束冲突则视为已入库正常返回，
    /// 消除 check-then-insert 的 TOCTOU 竞态。
    /// </summary>
    private async Task PersistDeadLetterCopyAsync(DeadLetterMessage message, CancellationToken ct)
    {
        var existing = await _repository.GetByOriginalMessageIdAsync(message.OriginalMessageId, ct);
        if (existing is not null)
        {
            _logger.LogDebug("死信消息 OriginalMessageId={OriginalMessageId} 已入库，跳过重复入库", message.OriginalMessageId);
            return;
        }

        try
        {
            await _repository.AddAsync(message, ct);
            await _unitOfWork.SaveEntitiesAsync(ct);

            _logger.LogInformation("死信消息 {MessageId}（OriginalMessageId={OriginalMessageId}）已入库副本",
                message.MessageId, message.OriginalMessageId);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // 并发插入导致唯一索引冲突，视为已入库，正常返回
            _logger.LogWarning(ex,
                "死信消息 OriginalMessageId={OriginalMessageId} 并发插入冲突，已按幂等处理", message.OriginalMessageId);
        }
    }

    /// <summary>
    /// 判断 DbUpdateException 是否为唯一约束冲突（SQL Server 错误码 2601/2627），
    /// 同时匹配索引名 ix_dead_letter_messages_original_message_id 与通用关键字作为兜底，
    /// 兼容 PostgreSQL/MySQL 等其他数据库的错误消息。
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
        // PostgreSQL: duplicate key value violates unique constraint
        // MySQL: Duplicate entry
        return message.Contains("2601", StringComparison.Ordinal)
            || message.Contains("2627", StringComparison.Ordinal)
            || message.Contains("ix_dead_letter_messages_original_message_id", StringComparison.OrdinalIgnoreCase)
            || message.Contains("UNIQUE KEY", StringComparison.OrdinalIgnoreCase)
            || message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Duplicate entry", StringComparison.OrdinalIgnoreCase);
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

                // message_id 缺失时回退到 delivery_tag，确保 OriginalMessageId 非空（实体校验要求）
                if (string.IsNullOrWhiteSpace(originalMessageId))
                {
                    if (item.TryGetProperty("delivery_tag", out var deliveryTag))
                    {
                        originalMessageId = deliveryTag.GetRawText();
                    }
                    else
                    {
                        originalMessageId = messageId.ToString("N");
                    }
                }

                // error_reason 缺失时填默认值（实体校验要求非空）
                if (string.IsNullOrWhiteSpace(errorReason))
                {
                    errorReason = "unknown (no x-death header)";
                }

                var message = DeadLetterMessage.Create(
                    messageId,
                    originalMessageId,
                    sourceContext ?? "rabbitmq",
                    string.IsNullOrWhiteSpace(originalTopic) ? "unknown" : originalTopic,
                    payload,
                    string.IsNullOrWhiteSpace(headers) ? "{}" : headers,
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
