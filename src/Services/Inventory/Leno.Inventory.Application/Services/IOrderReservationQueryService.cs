using Leno.SharedContracts.Integration.Inventory;

namespace Leno.Inventory.Application.Services;

/// <summary>
/// 订单库存预占明细查询服务接口。
/// 由 Inventory BC 内部使用，在收到 <see cref="ConfirmStockCommand"/> / <see cref="ReleaseStockCommand"/>
/// （这两类命令不携带 SKU 明细）时，按 OrderId 从 Redis 权威预占层查询该订单的全部预占明细。
/// </summary>
public interface IOrderReservationQueryService
{
    /// <summary>
    /// 查询指定订单的全部预占明细（SkuId + Quantity）。
    /// 实现基于 Redis SCAN 模式匹配 <c>inventory:reserved:*:{orderId}</c>。
    /// </summary>
    /// <param name="orderId">关联订单标识。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>预占明细列表；订单无预占返回空列表。</returns>
    Task<IReadOnlyList<ReserveStockItem>> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default);
}
