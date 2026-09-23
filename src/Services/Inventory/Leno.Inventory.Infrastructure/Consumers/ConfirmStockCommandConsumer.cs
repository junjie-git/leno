using Leno.Inventory.Application;
using Leno.SharedContracts.Integration.Inventory;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Leno.Inventory.Infrastructure.Consumers;

/// <summary>
/// 库存确认扣减命令消费者（Order BC → Inventory BC）。
/// 消费 <see cref="ConfirmStockCommand"/>，调用 <see cref="IInventoryAppService.ConfirmAsync"/>。
/// 命令按订单寻址（不携带明细）—— 台账即明细，应用服务按 orderId 解析；
/// AppService 内部三层幂等（幂等存储 / 台账唯一约束 / 状态机），本消费者不重复检查。
/// </summary>
public sealed class ConfirmStockCommandConsumer : IConsumer<ConfirmStockCommand>
{
    private readonly IInventoryAppService _inventoryAppService;
    private readonly ILogger<ConfirmStockCommandConsumer> _logger;

    public ConfirmStockCommandConsumer(
        IInventoryAppService inventoryAppService,
        ILogger<ConfirmStockCommandConsumer> logger)
    {
        ArgumentNullException.ThrowIfNull(inventoryAppService);
        ArgumentNullException.ThrowIfNull(logger);
        _inventoryAppService = inventoryAppService;
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

        await _inventoryAppService.ConfirmAsync(command.OrderId, command.IdempotencyKey, ct);

        _logger.LogInformation("库存确认命令处理完成 OrderId={OrderId}", command.OrderId);
    }
}
