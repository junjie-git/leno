namespace Leno.Product.Application.DTOs;

/// <summary>
/// 库存补货 DTO。
/// </summary>
public sealed class ReplenishStockDto
{
    /// <summary>补货数量，须 > 0。</summary>
    public int Quantity { get; init; }
}
