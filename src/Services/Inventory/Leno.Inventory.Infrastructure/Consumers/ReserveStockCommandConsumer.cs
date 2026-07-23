using Leno.Infrastructure.Abstractions;
using Leno.Inventory.Application;
using Leno.SharedContracts.Integration.Inventory;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Leno.Inventory.Infrastructure.Consumers;

/// <summary>
/// 库存预占命令消费者（Order BC → Inventory BC）。
/// 消费 <see cref="ReserveStockCommand"/>，调用 <see cref="IInventoryAppService.ReserveAsync"/>。
/// 双轨期：当 Inventory:UseExternalBc=true 时，Order BC 经 MassTransit 发布命令由本消费者消费；
/// flag=false 时，Order BC 通过进程内 IInventoryAppService 直接调用。
/// 命令本身携带 IdempotencyKey，AppService 内部已做幂等去重，本消费者不再重复幂等检查。
/// </summary>
public sealed class ReserveStockCommandConsumer : IConsumer<ReserveStockCommand>
{
    private readonly IInventoryAppService _inventoryAppService;
    private readonly ILogger<ReserveStockCommandConsumer> _logger;

    public ReserveStockCommandConsumer(
        IInventoryAppService inventoryAppService,
        ILogger<ReserveStockCommandConsumer> logger)
    {
        ArgumentNullException.ThrowIfNull(inventoryAppService);
        ArgumentNullException.ThrowIfNull(logger);
        _inventoryAppService = inventoryAppService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<ReserveStockCommand> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var command = context.Message;
        var ct = context.CancellationToken;

        _logger.LogInformation("收到库存预占命令 OrderId={OrderId} ItemCount={Count} IdempotencyKey={Key}",
            command.OrderId, command.Items.Count, command.IdempotencyKey);

        var result = await _inventoryAppService.ReserveAsync(
            command.OrderId,
            command.Items,
            command.IdempotencyKey,
            ct);

        if (!result.Success)
        {
            _logger.LogWarning("库存预占失败 OrderId={OrderId} Reason={Reason}",
                command.OrderId, result.FailureReason);
            // 预占失败：通过 respond 抛出异常（如果有 response 地址），由 Order Saga 处理
            // MassTransit 会将异常传递给 RequestClient（如果使用 request/response 模式）
            throw new InvalidOperationException(
                $"库存预占失败 OrderId={command.OrderId} Reason={result.FailureReason}");
        }

        _logger.LogInformation("库存预占命令处理完成 OrderId={OrderId} ReservedCount={Count}",
            command.OrderId, result.ReservedItems.Count);
    }
}
