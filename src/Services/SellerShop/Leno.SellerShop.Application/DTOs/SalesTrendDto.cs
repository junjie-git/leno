namespace Leno.SellerShop.Application.DTOs;

/// <summary>
/// 销售趋势 DTO，用于工作台趋势图表按日展示。
/// </summary>
public sealed class SalesTrendDto
{
    public DateOnly Date { get; init; }

    public int OrderCount { get; init; }

    public decimal SalesAmount { get; init; }

    public string SalesCurrency { get; init; } = "CNY";

    public decimal AvgRating { get; init; }
}
