namespace Leno.Order.Application.Messages;

/// <summary>
/// 订单超时延迟消息，由延迟队列在支付截止时间后投递。
/// 非 IIntegrationEvent，为普通 MassTransit 消息。
/// </summary>
public record OrderTimeoutMessage(Guid OrderId);