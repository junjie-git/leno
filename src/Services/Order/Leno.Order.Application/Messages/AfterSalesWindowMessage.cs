namespace Leno.Order.Application.Messages;

/// <summary>
/// 售后窗口结束延迟消息，由延迟队列在售后窗口结束时间后投递。
/// 非 IIntegrationEvent，为普通 MassTransit 消息。
/// </summary>
public record AfterSalesWindowMessage(Guid OrderId);