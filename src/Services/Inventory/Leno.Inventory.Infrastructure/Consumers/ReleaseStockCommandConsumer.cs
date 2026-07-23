using Leno.Inventory.Application;
using Leno.Inventory.Application.Services;
using Leno.SharedContracts.Integration.Inventory;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Leno.Inventory.Infrastructure.Consumers;

/// <summary>
/// 库存释放命令消费者（Order BC → Inventory BC）。
/// 消费 <see cref="ReleaseStockCommand"/>，按 <see cref="ReleaseStockCommand.OperationType"/> 分发：
/// - <see cref="ReleaseStockOperationType.Release"/>：调用 <see cref="IInventoryAppService.ReleaseAsync"/>（释放预占）
/// - <see cref="ReleaseStockOperationType.ReturnDeducted"/>：调用 <see cref="IInventoryAppService.ReturnDeductedAsync"/>（归还已扣减）
/// 命令不携带 SKU 明细，通过 <see cref="IOrderReservationQueryService"/> 从 Redis 查询预占明细。
/// AppService 内部已做幂等去重。
/// </summary>
public sealed class ReleaseStockCommandConsumer : IConsumer<ReleaseStockCommand>
{
    private readonly IInventoryAppService _inventoryAppService;
    private readonly IOrderReservationQueryService _orderReservationQueryService;
    private readonly ILogger<ReleaseStockCommandConsumer> _logger;

    public ReleaseStockCommandConsumer(
        IInventoryAppService inventoryAppService,
        IOrderReservationQueryService orderReservationQueryService,
        ILogger<ReleaseStockCommandConsumer> logger)
    {
        ArgumentNullException.ThrowIfNull(inventoryAppService);
        ArgumentNullException.ThrowIfNull(orderReservationQueryService);
        ArgumentNullException.ThrowIfNull(logger);
        _inventoryAppService = inventoryAppService;
        _orderReservationQueryService = orderReservationQueryService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<ReleaseStockCommand> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var command = context.Message;
        var ct = context.CancellationToken;

        _logger.LogInformation("收到库存释放命令 OrderId={OrderId} OperationType={OperationType} IdempotencyKey={Key}",
            command.OrderId, command.OperationType, command.IdempotencyKey);

        // 命令不携带 SKU 明细，从 Redis 查询该订单的全部预占明细
        var items = await _orderReservationQueryService.GetByOrderIdAsync(command.OrderId, ct);

        if (items.Count == 0)
        {
            _logger.LogWarning("库存释放：订单无预占明细，跳过 OrderId={OrderId}", command.OrderId);
            return;
        }

        switch (command.OperationType)
        {
            case ReleaseStockOperationType.Release:
                await _inventoryAppService.ReleaseAsync(command.OrderId, items, command.IdempotencyKey, ct);
                break;
            case ReleaseStockOperationType.ReturnDeducted:
                await _inventoryAppService.ReturnDeductedAsync(command.OrderId, items, command.IdempotencyKey, ct);
                break;
            default:
                throw new InvalidOperationException(
                    $"未知的释放操作类型 {command.OperationType}，OrderId={command.OrderId}");
        }

        _logger.LogInformation("库存释放命令处理完成 OrderId={OrderId} OperationType={OperationType} ItemCount={Count}",
            command.OrderId, command.OperationType, items.Count);
    }
}
