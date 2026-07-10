namespace Leno.Order.Application.Services;

/// <summary>
/// 商品域防腐层服务接口，下单时查询商品域 SKU 当前信息用于构建商品快照与库存校验。
/// 接口定义在应用层，实现位于基础设施层，屏蔽商品域内部模型。
/// </summary>
public interface IProductAntiCorruptionService
{
    /// <summary>
    /// 查询 SKU 当前信息。
    /// </summary>
    /// <param name="skuId">SKU 标识。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>SKU 信息，不存在返回 null。</returns>
    Task<SkuInfo?> GetSkuInfoAsync(Guid skuId, CancellationToken ct = default);
}

/// <summary>
/// SKU 信息传输对象，承载下单所需的商品域当前数据。
/// </summary>
public sealed class SkuInfo
{
    public Guid SkuId { get; set; }

    public Guid SpuId { get; set; }

    public Guid SellerId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public string SkuName { get; set; } = string.Empty;

    public string? MainImage { get; set; }

    public decimal UnitPrice { get; set; }

    public int AvailableQty { get; set; }

    public bool IsOnSale { get; set; }
}
