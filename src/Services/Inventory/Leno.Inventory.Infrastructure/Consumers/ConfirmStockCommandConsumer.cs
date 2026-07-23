using Leno.Infrastructure.Abstractions;
using Leno.Inventory.Application;
using Leno.Inventory.Application.Services;
using Leno.SharedContracts.Integration.Inventory;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Leno.Inventory.Infrastructure.Consumers;

/// <summary>
/// 库存确认扣减命令消费者（Order BC → Inventory BC）。
/// 消费 <see cref="ConfirmStockCommand"/>，先通过 <see cref="IOrderReservationQueryService"/> 从 Redis
/// 查询该订单的全部预占明细（命令本身不携带 SKU 明细），再调用 <see cref="IInventoryAppService.ConfirmAsync"/>。
/// AppService 内部已做幂等去重。
/// </summary>
public sealed class ConfirmStockCommandConsumer : IConsumer<ConfirmStockCommand>
{
    private readonly IInventoryAppService _inventoryAppService;
    private readonly IOrderReservationQueryService _orderReservationQueryService;
    private readonly ILogger<ConfirmStockCommandConsumer> _logger;

    public ConfirmStockCommandConsumer(
        IInventoryAppService inventoryAppService,
        IOrderReservationQueryService orderReservationQueryService,
        ILogger<ConfirmStockCommandConsumer> logger)
    {
        ArgumentNullException.ThrowIfNull(inventoryAppService);
        ArgumentNullException.ThrowIfNull(orderReservationQueryService);
        ArgumentNullException.ThrowIfNull(logger);
        _inventoryAppService = inventoryAppService;
        _orderReservationQueryService = orderReservationQueryService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<ConfirmStockCommand> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var command = context.Message;
        var ct = context.CancellationToken;

        _logger.LogInformation("收到库存确认命令 OrderId={OrderId} IdempotencyKey={Key}",
            command.OrderId, command.IdempotencyKey);

        // 命令不携带 SKU 明细，从 Redis 查询该订单的全部预占明细
        var items = await _orderReservationQueryService.GetByOrderIdAsync(command.OrderId, ct);

        if (items.Count == 0)
        {
            _logger.LogWarning("库存确认：订单无预占明细，跳过 OrderId={OrderId}", command.OrderId);
            return;
        }

        await _inventoryAppService.ConfirmAsync(command.OrderId, items, command.IdempotencyKey, ct);

        _logger.LogInformation("库存确认命令处理完成 OrderId={OrderId} ItemCount={Count}",
            command.OrderId, items.Count);
    }
}
