using MassTransit;

namespace Leno.SharedContracts.Integration.Inventory;

/// <summary>
/// 库存确认扣减命令（Order BC → Inventory BC）。
/// 由 Order BC 在支付成功后发布，Inventory BC 消费后调用 <c>IInventoryAppService.ConfirmAsync</c>。
/// </summary>
public sealed record ConfirmStockCommand(
    Guid OrderId,
    Guid IdempotencyKey) : CorrelatedBy<Guid>
{
    /// <summary>关联标识，与 <see cref="OrderId"/> 一致。</summary>
    public Guid CorrelationId => OrderId;
}
