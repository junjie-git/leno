using Leno.Order.Domain.Aggregates;
using Leno.Order.Domain.Repositories;
using OrderAggregate = Leno.Order.Domain.Aggregates.Order;

namespace Leno.Order.Application.Services;

/// <summary>
/// 订单域内部查询服务实现，供售后/评价域跨域资格校验使用。
/// 仅读取订单聚合的状态与明细快照，不触发任何状态变更。
/// </summary>
public sealed class OrderInternalQueryService : IOrderInternalQueryService
{
    private readonly IOrderRepository _orderRepository;

    public OrderInternalQueryService(IOrderRepository orderRepository)
    {
        ArgumentNullException.ThrowIfNull(orderRepository);
        _orderRepository = orderRepository;
    }

    /// <inheritdoc />
    public async Task<OrderStatusResultDto?> GetOrderStatusAsync(Guid orderId, CancellationToken ct = default)
    {
        var order = await _orderRepository.GetByIdAsync(orderId, ct);
        if (order is null)
        {
            return null;
        }

        return ToResultDto(order);
    }

    /// <inheritdoc />
    public async Task<Guid?> GetOrderSellerIdAsync(Guid orderId, CancellationToken ct = default)
    {
        var order = await _orderRepository.GetByIdAsync(orderId, ct).ConfigureAwait(false);
        return order?.SellerId;
    }

    private static OrderStatusResultDto ToResultDto(OrderAggregate order)
    {
        var items = new List<OrderItemStatusDto>(order.Items.Count);
        foreach (var item in order.Items)
        {
            items.Add(new OrderItemStatusDto
            {
                OrderLineId = item.Id,
                SkuId = item.SkuId,
                Quantity = item.Quantity,
                // 当前 OrderItem 聚合未承载售后状态字段，跨域查询视为无进行中售后
                AfterSalesStatus = 0
            });
        }

        return new OrderStatusResultDto
        {
            OrderId = order.Id,
            Status = (int)order.Status,
            UserId = order.UserId,
            CompletedAt = order.CompletedAt ?? DateTime.MinValue,
            CreatedAt = order.CreatedAt,
            Items = items
        };
    }
}
