using Leno.ApiGateway.Bff.Models;
using Microsoft.AspNetCore.Mvc;

namespace Leno.ApiGateway.Bff.Controllers;

/// <summary>
/// 商家工作台 BFF 聚合端点。
/// <para>
/// 并行调用 SellerShop BC（店铺看板）、Order BC（最近订单）、ReviewAfterSales BC（评论统计），
/// 聚合返回 <see cref="SellerDashboardBffResponse"/>。
/// </para>
/// </summary>
[ApiController]
[Route("api/bff/seller/{sellerId:guid}/dashboard")]
public sealed class SellerDashboardBffController : ControllerBase
{
    private const string ShopDashboardSource = "shop-dashboard";
    private const string RecentOrdersSource = "recent-orders";
    private const string ReviewStatsSource = "review-stats";

    private const string SellerShopServiceBase = "http://sellershop-api:8080";
    private const string OrderServiceBase = "http://order-api:8080";
    private const string ReviewServiceBase = "http://reviewaftersales-api:8080";

    private readonly IBffForwarderService _forwarder;
    private readonly ILogger<SellerDashboardBffController> _logger;

    public SellerDashboardBffController(
        IBffForwarderService forwarder,
        ILogger<SellerDashboardBffController> logger)
    {
        _forwarder = forwarder ?? throw new ArgumentNullException(nameof(forwarder));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 聚合查询商家工作台：店铺看板 + 最近订单 + 评论统计。
    /// </summary>
    /// <param name="sellerId">商家 ID（GUID）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>
    /// 200 OK + <see cref="BffResponse{T}"/>；部分下游失败时 Partial=true，Errors 含失败明细。
    /// </returns>
    [HttpGet]
    public async Task<ActionResult<BffResponse<SellerDashboardBffResponse>>> Get(
        [FromRoute] Guid sellerId,
        CancellationToken ct)
    {
        var requestId = HttpContext.TraceIdentifier;
        var requests = new BffDownstreamRequest[]
        {
            new()
            {
                Source = ShopDashboardSource,
                ServiceUrl = $"{SellerShopServiceBase}/api/seller/{sellerId:D}/dashboard"
            },
            new()
            {
                Source = RecentOrdersSource,
                ServiceUrl = $"{OrderServiceBase}/api/orders?sellerId={sellerId:D}&pageIndex=0&pageSize=5"
            },
            new()
            {
                Source = ReviewStatsSource,
                ServiceUrl = $"{ReviewServiceBase}/api/reviews?sellerId={sellerId:D}"
            }
        };

        _logger.LogInformation(
            "BFF SellerDashboard: forwarding {Count} downstream requests for sellerId={SellerId}, requestId={RequestId}",
            requests.Length, sellerId, requestId);

        var response = await _forwarder.ForwardAsync(
            requestId,
            requests,
            dict => new SellerDashboardBffResponse
            {
                ShopDashboard = dict.GetValueOrDefault(ShopDashboardSource),
                RecentOrders = dict.GetValueOrDefault(RecentOrdersSource),
                ReviewStats = dict.GetValueOrDefault(ReviewStatsSource)
            },
            ct);

        return Ok(response);
    }
}
