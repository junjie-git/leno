using System.Text.Json;
using Leno.ApiGateway.Bff.Models;
using Microsoft.AspNetCore.Mvc;

namespace Leno.ApiGateway.Bff.Controllers;

/// <summary>
/// 购物车结算预览 BFF 聚合端点。
/// <para>
/// 并行调用 Cart BC（购物车预览）、Promotion BC（优惠计算）、PointsMembership BC（积分试算抵扣），
/// 聚合返回 <see cref="CartCheckoutPreviewBffResponse"/>。
/// </para>
/// </summary>
[ApiController]
[Route("api/bff/cart/checkout-preview")]
public sealed class CartCheckoutPreviewBffController : ControllerBase
{
    private const string CartPreviewSource = "cart-preview";
    private const string PromotionSource = "promotion";
    private const string PointsTrialOffsetSource = "points-trial-offset";

    private const string CartServiceBase = "http://cart-api:8080";
    private const string PromotionServiceBase = "http://promotion-api:8080";
    private const string PointsServiceBase = "http://pointsmembership-api:8080";

    private readonly IBffForwarderService _forwarder;
    private readonly ILogger<CartCheckoutPreviewBffController> _logger;

    public CartCheckoutPreviewBffController(
        IBffForwarderService forwarder,
        ILogger<CartCheckoutPreviewBffController> logger)
    {
        _forwarder = forwarder ?? throw new ArgumentNullException(nameof(forwarder));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 聚合查询购物车结算预览：购物车 + 优惠 + 积分抵扣。
    /// </summary>
    /// <param name="request">结算预览请求体（透传至三个下游）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>
    /// 200 OK + <see cref="BffResponse{T}"/>；部分下游失败时 Partial=true，Errors 含失败明细。
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

        var requests = new BffDownstreamRequest[]
        {
            new()
            {
                Source = CartPreviewSource,
                Method = "POST",
                ServiceUrl = $"{CartServiceBase}/api/cart/preview",
                RequestBody = body
            },
            new()
            {
                Source = PromotionSource,
                Method = "POST",
                ServiceUrl = $"{PromotionServiceBase}/internal/v1/promotions/calculate",
                RequestBody = body
            },
            new()
            {
                Source = PointsTrialOffsetSource,
                Method = "POST",
                ServiceUrl = $"{PointsServiceBase}/internal/v1/points/trial-offset",
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

        return Ok(response);
    }
}
