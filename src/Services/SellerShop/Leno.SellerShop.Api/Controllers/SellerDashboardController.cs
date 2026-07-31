using Leno.Infrastructure.Abstractions.Cqrs;
using Leno.Infrastructure.Auth;
using Leno.SharedContracts.Responses;
using Leno.SellerShop.Application;
using Leno.SellerShop.Application.DTOs;
using Leno.SellerShop.Application.Options;
using Leno.SellerShop.Application.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.SellerShop.Api.Controllers;

/// <summary>
/// 卖家工作台控制器，提供工作台概览、销售趋势与运营指标查询端点。
/// 全部端点需认证，数据范围限定为当前卖家自己的店铺。
/// </summary>
[Authorize]
[ApiController]
[Route("api/seller")]
public sealed class SellerDashboardController : SellerShopControllerBase
{
    private readonly ISellerDashboardAppService _dashboardAppService;
    private readonly IShopAppService _shopAppService;
    private readonly IQueryHandler<ShopDashboardQuery, ShopDashboardResult?> _dashboardQueryHandler;
    private readonly DashboardFeatureOptions _dashboardOptions;
    private readonly ILogger<SellerDashboardController> _logger;

    public SellerDashboardController(
        ICurrentUserContext currentUser,
        ISellerDashboardAppService dashboardAppService,
        IShopAppService shopAppService,
        IQueryHandler<ShopDashboardQuery, ShopDashboardResult?> dashboardQueryHandler,
        IOptions<DashboardFeatureOptions> dashboardOptions,
        ILogger<SellerDashboardController> logger)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(dashboardAppService);
        ArgumentNullException.ThrowIfNull(shopAppService);
        ArgumentNullException.ThrowIfNull(dashboardQueryHandler);
        ArgumentNullException.ThrowIfNull(dashboardOptions);
        ArgumentNullException.ThrowIfNull(logger);
        _dashboardAppService = dashboardAppService;
        _shopAppService = shopAppService;
        _dashboardQueryHandler = dashboardQueryHandler;
        _dashboardOptions = dashboardOptions.Value;
        _logger = logger;
    }

    /// <summary>查询当前卖家工作台概览（店铺信息 + 当日运营指标）。</summary>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(ApiResponse<SellerDashboardDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboardAsync(CancellationToken ct)
    {
        var userId = GetCurrentUserId();

        if (_dashboardOptions.EnableComparison)
        {
            return await GetDashboardWithComparisonAsync(userId, ct);
        }

        if (_dashboardOptions.UseReadModel)
        {
            var dto = await GetDashboardFromReadModelAsync(userId, ct);
            if (dto is not null)
            {
                return Ok(ApiResponse.Success(dto));
            }

            // ES 读模型无数据时回退到 DB
            _logger.LogWarning("ES 读模型无数据，回退到 DB 路径，sellerId={SellerId}", userId);
        }

        var dashboard = await _dashboardAppService.GetDashboardAsync(userId, ct);
        return Ok(ApiResponse.Success(dashboard));
    }

    /// <summary>
    /// 双发对比模式：同时调用 DB 与 ES 两条路径，对比关键字段差异并记录 Warning 日志。
    /// 灰度期以 DB 结果为基准返回。
    /// </summary>
    private async Task<IActionResult> GetDashboardWithComparisonAsync(Guid userId, CancellationToken ct)
    {
        // 先加载店铺获取 ShopId（ES 查询需要）
        var shop = await _shopAppService.GetMyShopAsync(userId, ct);

        var dbTask = _dashboardAppService.GetDashboardAsync(userId, ct);
        var esTask = _dashboardQueryHandler.HandleAsync(new ShopDashboardQuery { ShopId = shop.Id }, ct);

        await Task.WhenAll(dbTask, esTask);

        var dbResult = await dbTask;
        var esResult = await esTask;

        if (esResult is not null)
        {
            if (dbResult.TotalOrders != esResult.TotalOrders)
            {
                _logger.LogWarning(
                    "Dashboard 双发对比差异：TotalOrders DB={DbOrders} ES={EsOrders}，ShopId={ShopId}",
                    dbResult.TotalOrders, esResult.TotalOrders, shop.Id);
            }

            if (dbResult.TotalRevenue != esResult.TotalSales)
            {
                _logger.LogWarning(
                    "Dashboard 双发对比差异：TotalRevenue DB={DbRevenue} ES TotalSales={EsSales}，ShopId={ShopId}",
                    dbResult.TotalRevenue, esResult.TotalSales, shop.Id);
            }
        }
        else
        {
            _logger.LogWarning("Dashboard 双发对比：ES 读模型无数据，ShopId={ShopId}", shop.Id);
        }

        return Ok(ApiResponse.Success(dbResult));
    }

    /// <summary>
    /// 从 ES 读模型获取 Dashboard 数据，合并当日指标。
    /// 返回 null 表示 ES 中无对应文档，需回退到 DB。
    /// </summary>
    private async Task<SellerDashboardDto?> GetDashboardFromReadModelAsync(Guid userId, CancellationToken ct)
    {
        var shop = await _shopAppService.GetMyShopAsync(userId, ct);

        var esResult = await _dashboardQueryHandler.HandleAsync(new ShopDashboardQuery { ShopId = shop.Id }, ct);
        if (esResult is null)
        {
            return null;
        }

        // 当日指标从 ShopMetrics 获取（ES 读模型不含当日数据）
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var metricsList = await _dashboardAppService.GetShopMetricsAsync(shop.Id, today, today, ct);
        var todayMetrics = metricsList.FirstOrDefault();

        return new SellerDashboardDto
        {
            ShopId = shop.Id,
            ShopName = esResult.ShopName,
            Status = shop.Status,
            ProductCount = shop.ProductCount,
            TotalOrders = esResult.TotalOrders,
            PendingOrders = esResult.PendingOrders,
            CompletedOrders = esResult.CompletedOrders,
            TotalRevenue = esResult.TotalSales,
            TodayOrderCount = todayMetrics?.OrderCount ?? 0,
            TodaySalesAmount = todayMetrics?.SalesAmount ?? 0m,
            TodaySalesCurrency = todayMetrics?.SalesCurrency ?? "CNY",
            TodayAvgRating = todayMetrics?.AvgRating ?? 0m,
            TodayRatingCount = todayMetrics?.RatingCount ?? 0,
            TodayRefundCount = todayMetrics?.RefundCount ?? 0
        };
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
