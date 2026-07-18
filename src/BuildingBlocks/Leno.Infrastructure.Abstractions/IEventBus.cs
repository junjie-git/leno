namespace Leno.Infrastructure.Abstractions;

/// <summary>
/// 事件总线抽象，发布跨上下文集成事件。
/// 实现位于基础设施层（基于 MassTransit / RabbitMQ）。
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// 发布集成事件到消息总线。订阅由各上下文在 DI 注册时配置消费者完成。
    /// </summary>
    /// <typeparam name="T">集成事件类型，须实现 <c>IIntegrationEvent</c>。</typeparam>
    Task PublishAsync<T>(T integrationEvent, CancellationToken ct = default) where T : notnull;

    /// <summary>
    /// 发布集成事件到消息总线，并附加消息头（M4.2 起 Outbox 携带 <c>schema-version</c> 等元数据）。
    /// 实现需将 headers 写入底层 MQ 消息头（MassTransit PublishContext.Headers）。
    /// </summary>
    /// <param name="headers">消息头键值对；null 或空字典等价于无附加 header。</param>
    Task PublishAsync<T>(T integrationEvent, IReadOnlyDictionary<string, string?>? headers, CancellationToken ct = default) where T : notnull;
}
