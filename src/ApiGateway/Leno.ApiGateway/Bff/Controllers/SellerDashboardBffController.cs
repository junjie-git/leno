using Leno.ApiGateway.Bff.Models;
using Microsoft.AspNetCore.Mvc;

namespace Leno.ApiGateway.Bff.Controllers;

/// <summary>
/// 商家工作台 BFF 聚合端点。
/// <para>
/// 并行调用 SellerShop BC（店铺看板）、Order BC（最近订单）、Review BC（评论统计），
/// 聚合返回 <see cref="SellerDashboardBffResponse"/>。
/// </para>
/// <para>
/// 下游基址统一由 <see cref="IBffServiceResolver"/> 按 <c>deploy/services.manifest.json</c> 的服务名解析：
/// 本地开发取 <c>Bff:Services:&lt;name&gt;:BaseUrl</c>，compose / K8s 走 Consul 注册名（<c>leno-&lt;composeService&gt;</c>）。
/// 寻址失败或全部下游失败时返回与网关一致的 503 降级信封。
/// </para>
/// </summary>
[ApiController]
[Route("api/bff/seller/{sellerId:guid}/dashboard")]
public sealed class SellerDashboardBffController : ControllerBase
{
    private const string ShopDashboardSource = "shop-dashboard";
    private const string RecentOrdersSource = "recent-orders";
    private const string ReviewStatsSource = "review-stats";

    /// <summary>清单服务名：sellershop（Consul 注册名 leno-seller-shop-api）。</summary>
    private const string SellerShopServiceName = "sellershop";

    /// <summary>清单服务名：order（Consul 注册名 leno-order-api）。</summary>
    private const string OrderServiceName = "order";

    /// <summary>清单服务名：review（Consul 注册名 leno-review-api）。</summary>
    private const string ReviewServiceName = "review";

    private readonly IBffForwarderService _forwarder;
    private readonly IBffServiceResolver _serviceResolver;
    private readonly ILogger<SellerDashboardBffController> _logger;

    public SellerDashboardBffController(
        IBffForwarderService forwarder,
        IBffServiceResolver serviceResolver,
        ILogger<SellerDashboardBffController> logger)
    {
        _forwarder = forwarder ?? throw new ArgumentNullException(nameof(forwarder));
        _serviceResolver = serviceResolver ?? throw new ArgumentNullException(nameof(serviceResolver));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 聚合查询商家工作台：店铺看板 + 最近订单 + 评论统计。
    /// </summary>
    /// <param name="sellerId">商家 ID（GUID）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>
    /// 200 OK + <see cref="BffResponse{T}"/>；部分下游失败时 Partial=true，Errors 含失败明细；
    /// 寻址失败或全部下游失败时返回 503 降级信封。
    /// </returns>
    [HttpGet]
    public async Task<ActionResult<BffResponse<SellerDashboardBffResponse>>> Get(
        [FromRoute] Guid sellerId,
        CancellationToken ct)
    {
        var requestId = HttpContext.TraceIdentifier;

        string sellerShopBase;
        string orderBase;
        string reviewBase;
        try
        {
            sellerShopBase = await _serviceResolver
                .ResolveBaseAddressAsync(SellerShopServiceName, ct).ConfigureAwait(false);
            orderBase = await _serviceResolver
                .ResolveBaseAddressAsync(OrderServiceName, ct).ConfigureAwait(false);
            reviewBase = await _serviceResolver
                .ResolveBaseAddressAsync(ReviewServiceName, ct).ConfigureAwait(false);
        }
        catch (BffServiceUnavailableException ex)
        {
            _logger.LogError(
                ex,
                "BFF SellerDashboard 下游寻址失败：service={ServiceName}, consul={ConsulServiceName}, requestId={RequestId}",
                ex.ServiceName, ex.ConsulServiceName, requestId);
            return BffErrorEnvelope.DownstreamUnavailable();
        }

        var requests = new BffDownstreamRequest[]
        {
            new()
            {
                Source = ShopDashboardSource,
                ServiceUrl = $"{sellerShopBase}/api/seller/{sellerId:D}/dashboard"
            },
            new()
            {
                Source = RecentOrdersSource,
                ServiceUrl = $"{orderBase}/api/orders?sellerId={sellerId:D}&pageIndex=0&pageSize=5"
            },
            new()
            {
                Source = ReviewStatsSource,
                ServiceUrl = $"{reviewBase}/api/reviews?sellerId={sellerId:D}"
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

        // 全部下游失败时返回网关统一 503 信封，不返回 200 + 全 null 的空对象
        if (!response.Success && !response.Partial)
        {
            _logger.LogError(
                "BFF SellerDashboard 全部下游失败：sellerId={SellerId}, requestId={RequestId}, errors={Errors}",
                sellerId, requestId, string.Join("; ", response.Errors.Select(e => $"{e.Source}:{e.Message}")));
            return BffErrorEnvelope.DownstreamUnavailable();
        }

        return Ok(response);
    }
}
