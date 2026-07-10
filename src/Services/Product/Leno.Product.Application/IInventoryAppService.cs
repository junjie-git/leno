using Leno.Product.Application.DTOs;

namespace Leno.Product.Application;

/// <summary>
/// 库存管理应用服务，编排卖家/运营补货用例。
/// 补货发布 <c>StockAdjustedEvent</c> 通知订单域同步库存基线。
/// </summary>
public interface IInventoryAppService
{
    /// <summary>
    /// 卖家/运营为指定 SKU 补货。
    /// 不存在库存基线时按补货量初始化。
    /// </summary>
    Task ReplenishAsync(Guid skuId, ReplenishStockDto dto, CancellationToken ct = default);
}
