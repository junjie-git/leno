using MassTransit;

namespace Leno.SharedContracts.Integration.Inventory;

/// <summary>
/// 库存释放命令操作类型。
/// </summary>
public enum ReleaseStockOperationType
{
    /// <summary>释放预占（订单取消未支付）。</summary>
    Release = 0,

    /// <summary>归还已扣减（已支付/已发货订单强制取消）。</summary>
    ReturnDeducted = 1
}

/// <summary>
/// 库存释放命令（Order BC → Inventory BC）。
/// 由 Order BC 在订单取消或强制取消时发布，Inventory BC 消费后按
/// <see cref="OperationType"/> 调用 <c>IInventoryAppService.ReleaseAsync</c> 或 <c>ReturnDeductedAsync</c>。
/// </summary>
public sealed record ReleaseStockCommand(
    Guid OrderId,
    Guid IdempotencyKey,
    ReleaseStockOperationType OperationType = ReleaseStockOperationType.Release) : CorrelatedBy<Guid>
{
    /// <summary>关联标识，与 <see cref="OrderId"/> 一致。</summary>
    public Guid CorrelationId => OrderId;
}
