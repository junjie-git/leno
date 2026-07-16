using Leno.Infrastructure.Abstractions;
using Leno.SharedContracts.Events;
using Leno.SystemAdmin.Domain.Aggregates;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Leno.SystemAdmin.Infrastructure.Services;

/// <summary>
/// 死信重投共享逻辑。
/// 负责从 <see cref="DeadLetterMessage"/> 反序列化原始 <see cref="IIntegrationEvent"/> 并通过 <see cref="IEventBus"/> 重新发布。
/// 被 <see cref="DeadLetterQueueManager"/> 与 <see cref="RabbitMqDeadLetterManager"/> 共用，保证两者重投行为一致。
/// </summary>
internal static class DeadLetterRepublishHelper
{
    /// <summary>与 <see cref="Leno.Infrastructure.Outbox.OutboxPublisher{TDbContext}"/> 保持一致的反序列化选项（Web camelCase）。</summary>
    public static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>共享契约层程序集名候选，用于 <see cref="Type.GetType"/> 反查。</summary>
    private static readonly string[] SharedEventAssemblies =
    {
        "Leno.SharedContracts",
        "Leno.SharedKernel"
    };

    /// <summary>
    /// 解析死信消息的原始事件类型、反序列化 Payload 并通过事件总线重新发布。
    /// 任一环节失败抛 <see cref="InvalidOperationException"/>，调用方据此决定是否更新死信状态。
    /// </summary>
    public static async Task RepublishViaEventBusAsync(
        IEventBus eventBus,
        DeadLetterMessage message,
        ILogger logger,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(eventBus);
        ArgumentNullException.ThrowIfNull(message);

        var eventType = TryResolveEventType(message);
        if (eventType is null)
        {
            throw new InvalidOperationException(
                $"无法解析死信消息 {message.MessageId} 的事件类型"
                + $"（Headers={message.Headers}, OriginalTopic={message.OriginalTopic}）");
        }

        var payloadJson = UnwrapPayloadJson(message.Payload);
        var integrationEvent = JsonSerializer.Deserialize(payloadJson, eventType, SerializerOptions) as IIntegrationEvent;
        if (integrationEvent is null)
        {
            throw new InvalidOperationException(
                $"死信消息 {message.MessageId} 的 Payload 反序列化为 {eventType.FullName} 失败");
        }

        // 通过 IEventBus 重新发布到 MQ；MassTransit 按运行时类型路由到对应消费者。
        await eventBus.PublishAsync(integrationEvent, ct);

        logger.LogInformation(
            "死信消息 {MessageId} 已反序列化为 {EventType} 并通过事件总线重投",
            message.MessageId, eventType.FullName);
    }

    /// <summary>
    /// 从 <see cref="DeadLetterMessage.Headers"/>（MassTransit message-type 头）或 <see cref="DeadLetterMessage.OriginalTopic"/> 解析事件类型。
    /// 支持 MassTransit URN（urn:message:Leno.SharedContracts.Events:OrderCreatedEvent）与 .NET 全名两种格式。
    /// </summary>
    public static Type? TryResolveEventType(DeadLetterMessage message)
    {
        if (!string.IsNullOrWhiteSpace(message.Headers))
        {
            var typeFromHeaders = TryResolveFromHeaders(message.Headers);
            if (typeFromHeaders is not null)
            {
                return typeFromHeaders;
            }
        }

        return TryResolveTypeName(message.OriginalTopic);
    }

    private static Type? TryResolveFromHeaders(string headersJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(headersJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("message-type", out var messageType)
                && messageType.ValueKind == JsonValueKind.String)
            {
                return TryResolveTypeName(messageType.GetString());
            }
        }
        catch
        {
            // Headers 非 JSON 或解析失败，回退到 OriginalTopic
        }

        return null;
    }

    private static Type? TryResolveTypeName(string? typeNameOrUrn)
    {
        if (string.IsNullOrWhiteSpace(typeNameOrUrn))
        {
            return null;
        }

        var typeName = typeNameOrUrn.Trim();

        // MassTransit URN: urn:message:Leno.SharedContracts.Events:OrderCreatedEvent
        if (typeName.StartsWith("urn:message:", StringComparison.OrdinalIgnoreCase))
        {
            typeName = typeName["urn:message:".Length..].Replace(':', '.');
        }

        var resolved = Type.GetType(typeName);
        if (resolved is not null)
        {
            return resolved;
        }

        foreach (var assemblyName in SharedEventAssemblies)
        {
            resolved = Type.GetType($"{typeName}, {assemblyName}");
            if (resolved is not null)
            {
                return resolved;
            }
        }

        return null;
    }

    /// <summary>
    /// 兼容 RabbitMQ Management API 返回的 payload 既是裸 JSON 对象文本、也可能被存为带引号字符串字面量的两种情况。
    /// ParseDeadLetterMessages 当前用 GetRawText 存储 payload，若 payload 是 JSON 字符串则会带外层引号，需先反序列化为字符串去引号。
    /// </summary>
    public static string UnwrapPayloadJson(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return payload;
        }

        var trimmed = payload.Trim();
        if (trimmed.Length >= 2 && trimmed.StartsWith('"') && trimmed.EndsWith('"'))
        {
            try
            {
                return JsonSerializer.Deserialize<string>(trimmed) ?? payload;
            }
            catch
            {
                // 反序列化失败则保留原值
            }
        }

        return payload;
    }
}
