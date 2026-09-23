using Leno.Inventory.Application;
using Leno.Inventory.Application.Services;
using Leno.SharedContracts.Integration.Inventory;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Leno.Inventory.Infrastructure.Consumers;

/// <summary>
/// 库存释放命令消费者（Order BC → Inventory BC）。
/// 消费 <see cref="ReleaseStockCommand"/>，按 <see cref="ReleaseStockCommand.OperationType"/> 分发：
/// - <see cref="ReleaseStockOperationType.Release"/>：释放预占（订单取消/超时）
/// - <see cref="ReleaseStockOperationType.ReturnDeducted"/>：归还已扣减（已支付订单强制取消/退款）
/// 命令按订单寻址 —— 台账即明细，应用服务按 orderId 解析；AppService 内部三层幂等，本消费者不重复检查。
/// </summary>
public sealed class ReleaseStockCommandConsumer : IConsumer<ReleaseStockCommand>
{
    private readonly IInventoryAppService _inventoryAppService;
    private readonly ILogger<ReleaseStockCommandConsumer> _logger;

    public ReleaseStockCommandConsumer(
        IInventoryAppService inventoryAppService,
        ILogger<ReleaseStockCommandConsumer> logger)
    {
        ArgumentNullException.ThrowIfNull(inventoryAppService);
        ArgumentNullException.ThrowIfNull(logger);
        _inventoryAppService = inventoryAppService;
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

        await _inventoryAppService.ReleaseAsync(
            command.OrderId, command.OperationType, command.IdempotencyKey, ct);

        _logger.LogInformation("库存释放命令处理完成 OrderId={OrderId} OperationType={OperationType}",
            command.OrderId, command.OperationType);
    }
}
