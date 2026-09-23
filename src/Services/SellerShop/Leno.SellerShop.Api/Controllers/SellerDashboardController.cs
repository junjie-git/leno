using Leno.Infrastructure.Abstractions.Cqrs;
using Leno.Infrastructure.Auth;
using Leno.SharedContracts.Responses;
using Leno.SellerShop.Application;
using Leno.SellerShop.Application.DTOs;
using Leno.SellerShop.Application.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.SellerShop.Api.Controllers;

/// <summary>
/// 卖家工作台控制器，提供工作台概览、销售趋势与运营指标查询端点。
/// 全部端点需认证，数据范围限定为当前卖家自己的店铺。
/// <para>
/// 双轨下线 E2（2026-09-23）：Dashboard 数据源<b>固定为 ES 读模型</b> ——
/// 删除 <c>DashboardFeatureOptions</c>（UseReadModel/EnableComparison）与 DB 双发对比分支；
/// 累计指标读 ES（ShopDashboardQuery），当日指标与销售趋势保留 SQL 明细查询（ShopMetrics，强一致）；
/// ES 无文档（新店铺）时按零值返回，不再回退 DB 累计查询。
/// </para>
/// </summary>
[Authorize]
[ApiController]
[Route("api/seller")]
public sealed class SellerDashboardController : SellerShopControllerBase
{
    private readonly ISellerDashboardAppService _dashboardAppService;
    private readonly IShopAppService _shopAppService;
    private readonly IQueryHandler<ShopDashboardQuery, ShopDashboardResult?> _dashboardQueryHandler;

    public SellerDashboardController(
        ICurrentUserContext currentUser,
        ISellerDashboardAppService dashboardAppService,
        IShopAppService shopAppService,
        IQueryHandler<ShopDashboardQuery, ShopDashboardResult?> dashboardQueryHandler)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(dashboardAppService);
        ArgumentNullException.ThrowIfNull(shopAppService);
        ArgumentNullException.ThrowIfNull(dashboardQueryHandler);
        _dashboardAppService = dashboardAppService;
        _shopAppService = shopAppService;
        _dashboardQueryHandler = dashboardQueryHandler;
    }

    /// <summary>查询当前卖家工作台概览（店铺信息 + 累计指标（ES）+ 当日运营指标（SQL））。</summary>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(ApiResponse<SellerDashboardDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboardAsync(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var shop = await _shopAppService.GetMyShopAsync(userId, ct);

        // 累计指标：ES 读模型（事件派生）
        var esResult = await _dashboardQueryHandler.HandleAsync(new ShopDashboardQuery { ShopId = shop.Id }, ct);

        // 当日指标：ShopMetrics 按日明细（SQL，强一致单店查询，读模型不含按日数据）
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var metricsList = await _dashboardAppService.GetShopMetricsAsync(shop.Id, today, today, ct);
        var todayMetrics = metricsList.FirstOrDefault();

        var dto = new SellerDashboardDto
        {
            ShopId = shop.Id,
            ShopName = esResult?.ShopName ?? shop.ShopName,
            Status = shop.Status,
            ProductCount = shop.ProductCount,
            TotalOrders = esResult?.TotalOrders ?? 0,
            PendingOrders = esResult?.PendingOrders ?? 0,
            CompletedOrders = esResult?.CompletedOrders ?? 0,
            TotalRevenue = esResult?.TotalSales ?? 0m,
            TodayOrderCount = todayMetrics?.OrderCount ?? 0,
            TodaySalesAmount = todayMetrics?.SalesAmount ?? 0m,
            TodaySalesCurrency = todayMetrics?.SalesCurrency ?? "CNY",
            TodayAvgRating = todayMetrics?.AvgRating ?? 0m,
            TodayRatingCount = todayMetrics?.RatingCount ?? 0,
            TodayRefundCount = todayMetrics?.RefundCount ?? 0
        };
        return Ok(ApiResponse.Success(dto));
    }

    /// <summary>查询当前卖家店铺的销售趋势（按日序列）。</summary>
    [HttpGet("sales-trend")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SalesTrendDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSalesTrendAsync(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken ct)
    {
        var shop = await _shopAppService.GetMyShopAsync(GetCurrentUserId(), ct);
        var trend = await _dashboardAppService.GetSalesTrendAsync(shop.Id, from, to, ct);
        return Ok(ApiResponse.Success(trend));
    }

    /// <summary>查询当前卖家店铺的运营指标明细。</summary>
    [HttpGet("metrics")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ShopMetricsDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMetricsAsync(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken ct)
    {
        var shop = await _shopAppService.GetMyShopAsync(GetCurrentUserId(), ct);
        var metrics = await _dashboardAppService.GetShopMetricsAsync(shop.Id, from, to, ct);
        return Ok(ApiResponse.Success(metrics));
    }

    /// <summary>查询当前卖家店铺的低库存 SKU 列表（经 ACL 调商品域）。</summary>
    [HttpGet("dashboard/low-stock")]
    [ProducesResponseType(typeof(ApiResponse<List<LowStockItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLowStockAlertAsync(
        [FromQuery] int threshold = 10,
        CancellationToken ct = default)
    {
        var sellerId = GetCurrentUserId();
        var items = await _dashboardAppService.GetLowStockAlertAsync(sellerId, threshold, ct);
        return Ok(ApiResponse.Success(items));
    }
}
