using Leno.Infrastructure.Auth;
using Leno.SharedContracts.Responses;
using Leno.SellerShop.Application;
using Leno.SellerShop.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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

    public SellerDashboardController(
        ICurrentUserContext currentUser,
        ISellerDashboardAppService dashboardAppService,
        IShopAppService shopAppService)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(dashboardAppService);
        ArgumentNullException.ThrowIfNull(shopAppService);
        _dashboardAppService = dashboardAppService;
        _shopAppService = shopAppService;
    }

    /// <summary>查询当前卖家工作台概览（店铺信息 + 当日运营指标）。</summary>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(ApiResponse<SellerDashboardDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboardAsync(CancellationToken ct)
    {
        var dashboard = await _dashboardAppService.GetDashboardAsync(GetCurrentUserId(), ct);
        return Ok(ApiResponse.Success(dashboard));
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
}
