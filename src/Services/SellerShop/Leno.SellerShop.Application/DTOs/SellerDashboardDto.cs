using Leno.SellerShop.Domain.ValueObjects;

namespace Leno.SellerShop.Application.DTOs;

/// <summary>
/// 卖家工作台概览 DTO，聚合店铺基础信息与当日运营指标。
/// </summary>
public sealed class SellerDashboardDto
{
    public Guid ShopId { get; init; }

    public string ShopName { get; init; } = string.Empty;

    public ShopStatus Status { get; init; }

    /// <summary>在售商品数。</summary>
    public int ProductCount { get; init; }

    /// <summary>累计订单总数。</summary>
    public int TotalOrders { get; init; }

    /// <summary>待处理订单数。</summary>
    public int PendingOrders { get; init; }

    /// <summary>已完成订单数。</summary>
    public int CompletedOrders { get; init; }

    /// <summary>累计销售收入。</summary>
    public decimal TotalRevenue { get; init; }

    /// <summary>当日已完成订单数。</summary>
    public int TodayOrderCount { get; init; }

    /// <summary>当日销售额。</summary>
    public decimal TodaySalesAmount { get; init; }

    /// <summary>销售额币种（ISO 4217）。</summary>
    public string TodaySalesCurrency { get; init; } = "CNY";

    /// <summary>当日平均评分。</summary>
    public decimal TodayAvgRating { get; init; }

    /// <summary>当日评分次数。</summary>
    public int TodayRatingCount { get; init; }

    /// <summary>当日售后数。</summary>
    public int TodayRefundCount { get; init; }
}