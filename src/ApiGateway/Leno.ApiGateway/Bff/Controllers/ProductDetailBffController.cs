using Leno.ApiGateway.Bff.Models;
using Microsoft.AspNetCore.Mvc;

namespace Leno.ApiGateway.Bff.Controllers;

/// <summary>
/// 商品详情 BFF 聚合端点。
/// <para>
/// 并行调用 Product BC 的商品详情接口与 ReviewAfterSales BC 的评论摘要接口，
/// 聚合返回 <see cref="ProductDetailBffResponse"/>。
/// </para>
/// </summary>
[ApiController]
[Route("api/bff/products/{productId:guid}")]
public sealed class ProductDetailBffController : ControllerBase
{
    private const string ProductDetailSource = "product-detail";
    private const string ReviewSummarySource = "review-summary";

    private const string ProductServiceBase = "http://product-api:8080";
    private const string ReviewServiceBase = "http://reviewaftersales-api:8080";

    private readonly IBffForwarderService _forwarder;
    private readonly ILogger<ProductDetailBffController> _logger;

    public ProductDetailBffController(
        IBffForwarderService forwarder,
        ILogger<ProductDetailBffController> logger)
    {
        _forwarder = forwarder ?? throw new ArgumentNullException(nameof(forwarder));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 聚合查询商品详情与评论摘要。
    /// </summary>
    /// <param name="productId">商品 SPU ID（GUID）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>
    /// 200 OK + <see cref="BffResponse{T}"/>；部分下游失败时 Partial=true，Errors 含失败明细。
    /// </returns>
    [HttpGet]
    public async Task<ActionResult<BffResponse<ProductDetailBffResponse>>> Get(
        [FromRoute] Guid productId,
        CancellationToken ct)
    {
        var requestId = HttpContext.TraceIdentifier;
        var requests = new BffDownstreamRequest[]
        {
            new()
            {
                Source = ProductDetailSource,
                ServiceUrl = $"{ProductServiceBase}/api/products/{productId:D}"
            },
            new()
            {
                Source = ReviewSummarySource,
                ServiceUrl = $"{ReviewServiceBase}/api/reviews?spuId={productId:D}"
            }
        };

        _logger.LogInformation(
            "BFF ProductDetail: forwarding {Count} downstream requests for productId={ProductId}, requestId={RequestId}",
            requests.Length, productId, requestId);

        var response = await _forwarder.ForwardAsync(
            requestId,
            requests,
            dict => new ProductDetailBffResponse
            {
                Product = dict.GetValueOrDefault(ProductDetailSource),
                ReviewSummary = dict.GetValueOrDefault(ReviewSummarySource)
            },
            ct);

        return Ok(response);
    }
}
