namespace Leno.SellerShop.Application.DTOs;

/// <summary>
/// 店铺运营指标 DTO，按日返回订单、销售、商品数、评分与售后数据。
/// </summary>
public sealed class ShopMetricsDto
{
    public Guid ShopId { get; init; }

    public DateOnly Date { get; init; }

    public int OrderCount { get; init; }

    public decimal SalesAmount { get; init; }

    public string SalesCurrency { get; init; } = "CNY";

    public int ProductCount { get; init; }

    public decimal AvgRating { get; init; }

    public int RatingCount { get; init; }

    public int RefundCount { get; init; }
}
