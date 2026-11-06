using System.Text.Json;
using Leno.ApiGateway.Bff.Models;
using Microsoft.AspNetCore.Mvc;

namespace Leno.ApiGateway.Bff.Controllers;

/// <summary>
/// 购物车结算预览 BFF 聚合端点。
/// <para>
/// 并行调用 Cart BC（购物车预览）、Promotion BC（优惠计算）、Points BC（积分试算抵扣），
/// 聚合返回 <see cref="CartCheckoutPreviewBffResponse"/>。
/// </para>
/// <para>
/// 下游基址由 <see cref="IBffServiceResolver"/> 按清单服务名解析，不在控制器内硬编码。
/// 寻址失败或全部下游失败时返回与网关一致的 503 降级信封。
/// </para>
/// </summary>
[ApiController]
[Route("api/bff/cart/checkout-preview")]
public sealed class CartCheckoutPreviewBffController : ControllerBase
{
    private const string CartPreviewSource = "cart-preview";
    private const string PromotionSource = "promotion";
    private const string PointsTrialOffsetSource = "points-trial-offset";

    /// <summary>清单服务名：cart（Consul 注册名 leno-cart-api）。</summary>
    private const string CartServiceName = "cart";

    /// <summary>清单服务名：promotion（Consul 注册名 leno-promotion-api）。</summary>
    private const string PromotionServiceName = "promotion";

    /// <summary>清单服务名：points（Consul 注册名 leno-points-api）。</summary>
    private const string PointsServiceName = "points";

    private readonly IBffForwarderService _forwarder;
    private readonly IBffServiceResolver _serviceResolver;
    private readonly ILogger<CartCheckoutPreviewBffController> _logger;

    public CartCheckoutPreviewBffController(
        IBffForwarderService forwarder,
        IBffServiceResolver serviceResolver,
        ILogger<CartCheckoutPreviewBffController> logger)
    {
        _forwarder = forwarder ?? throw new ArgumentNullException(nameof(forwarder));
        _serviceResolver = serviceResolver ?? throw new ArgumentNullException(nameof(serviceResolver));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 聚合查询购物车结算预览：购物车 + 优惠 + 积分抵扣。
    /// </summary>
    /// <param name="request">结算预览请求体（透传至三个下游）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>
    /// 200 OK + <see cref="BffResponse{T}"/>；部分下游失败时 Partial=true，Errors 含失败明细；
    /// 寻址失败或全部下游失败时返回 503 降级信封。
    /// </returns>
    [HttpPost]
    public async Task<ActionResult<BffResponse<CartCheckoutPreviewBffResponse>>> Post(
        [FromBody] JsonElement? request,
        CancellationToken ct)
    {
        var requestId = HttpContext.TraceIdentifier;
        var body = request.HasValue
            ? request.Value.GetRawText()
            : "{}";

        string cartBase;
        string promotionBase;
        string pointsBase;
        try
        {
            cartBase = await _serviceResolver
                .ResolveBaseAddressAsync(CartServiceName, ct).ConfigureAwait(false);
            promotionBase = await _serviceResolver
                .ResolveBaseAddressAsync(PromotionServiceName, ct).ConfigureAwait(false);
            pointsBase = await _serviceResolver
                .ResolveBaseAddressAsync(PointsServiceName, ct).ConfigureAwait(false);
        }
        catch (BffServiceUnavailableException ex)
        {
            _logger.LogError(
                ex,
                "BFF CartCheckoutPreview 下游寻址失败：service={ServiceName}, consul={ConsulServiceName}, requestId={RequestId}",
                ex.ServiceName, ex.ConsulServiceName, requestId);
            return BffErrorEnvelope.DownstreamUnavailable();
        }

        var requests = new BffDownstreamRequest[]
        {
            new()
            {
                Source = CartPreviewSource,
                Method = "POST",
                ServiceUrl = $"{cartBase}/api/cart/preview",
                RequestBody = body
            },
            new()
            {
                Source = PromotionSource,
                Method = "POST",
                ServiceUrl = $"{promotionBase}/internal/v1/promotions/calculate",
                RequestBody = body
            },
            new()
            {
                Source = PointsTrialOffsetSource,
                Method = "POST",
                ServiceUrl = $"{pointsBase}/internal/v1/points/trial-offset",
                RequestBody = body
            }
        };

        _logger.LogInformation(
            "BFF CartCheckoutPreview: forwarding {Count} downstream requests, requestId={RequestId}",
            requests.Length, requestId);

        var response = await _forwarder.ForwardAsync(
            requestId,
            requests,
            dict => new CartCheckoutPreviewBffResponse
            {
                CartPreview = dict.GetValueOrDefault(CartPreviewSource),
                Promotion = dict.GetValueOrDefault(PromotionSource),
                PointsTrialOffset = dict.GetValueOrDefault(PointsTrialOffsetSource)
            },
            ct);

        // 全部下游失败时返回网关统一 503 信封，不返回 200 + 全 null 的空对象
        if (!response.Success && !response.Partial)
        {
            _logger.LogError(
                "BFF CartCheckoutPreview 全部下游失败：requestId={RequestId}, errors={Errors}",
                requestId, string.Join("; ", response.Errors.Select(e => $"{e.Source}:{e.Message}")));
            return BffErrorEnvelope.DownstreamUnavailable();
        }

        return Ok(response);
    }
}
