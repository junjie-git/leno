using Leno.SharedKernel.Abstractions;
using Leno.Infrastructure.Abstractions;
using MassTransit;

namespace Leno.Infrastructure.EventBus;

/// <summary>
/// 基于 MassTransit <see cref="IPublishEndpoint"/> 的事件总线实现。
/// RabbitMQ 拓扑（交换机、队列、路由键、死信队列）由 MassTransit 在 <c>UsingRabbitMq</c> 配置时按消息类型自动建立。
/// 发布到 RabbitMQ Topic 交换机，按集成事件类型路由。
/// </summary>
public sealed class RabbitMqEventBus : IEventBus
{
    private readonly IPublishEndpoint _publishEndpoint;

    public RabbitMqEventBus(IPublishEndpoint publishEndpoint)
    {
        ArgumentNullException.ThrowIfNull(publishEndpoint);
        _publishEndpoint = publishEndpoint;
    }

    /// <summary>
    /// 发布集成事件到消息总线。订阅由各上下文在 DI 注册消费者时配置完成。
    /// </summary>
    public async Task PublishAsync<T>(T integrationEvent, CancellationToken ct = default) where T : notnull
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        await _publishEndpoint.Publish(integrationEvent, ct);
    }
}
