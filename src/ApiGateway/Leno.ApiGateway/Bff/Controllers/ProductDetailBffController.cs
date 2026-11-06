using Leno.ApiGateway.Bff.Models;
using Microsoft.AspNetCore.Mvc;

namespace Leno.ApiGateway.Bff.Controllers;

/// <summary>
/// 商品详情 BFF 聚合端点。
/// <para>
/// 并行调用 Product BC 的商品详情接口与 Review BC 的评论摘要接口，
/// 聚合返回 <see cref="ProductDetailBffResponse"/>。
/// </para>
/// <para>
/// 下游基址由 <see cref="IBffServiceResolver"/> 按清单服务名解析，不在控制器内硬编码。
/// 寻址失败或全部下游失败时返回与网关一致的 503 降级信封。
/// </para>
/// </summary>
[ApiController]
[Route("api/bff/products/{productId:guid}")]
public sealed class ProductDetailBffController : ControllerBase
{
    private const string ProductDetailSource = "product-detail";
    private const string ReviewSummarySource = "review-summary";

    /// <summary>清单服务名：product（Consul 注册名 leno-product-api）。</summary>
    private const string ProductServiceName = "product";

    /// <summary>清单服务名：review（Consul 注册名 leno-review-api）。</summary>
    private const string ReviewServiceName = "review";

    private readonly IBffForwarderService _forwarder;
    private readonly IBffServiceResolver _serviceResolver;
    private readonly ILogger<ProductDetailBffController> _logger;

    public ProductDetailBffController(
        IBffForwarderService forwarder,
        IBffServiceResolver serviceResolver,
        ILogger<ProductDetailBffController> logger)
    {
        _forwarder = forwarder ?? throw new ArgumentNullException(nameof(forwarder));
        _serviceResolver = serviceResolver ?? throw new ArgumentNullException(nameof(serviceResolver));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 聚合查询商品详情与评论摘要。
    /// </summary>
    /// <param name="productId">商品 SPU ID（GUID）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>
    /// 200 OK + <see cref="BffResponse{T}"/>；部分下游失败时 Partial=true，Errors 含失败明细；
    /// 寻址失败或全部下游失败时返回 503 降级信封。
    /// </returns>
    [HttpGet]
    public async Task<ActionResult<BffResponse<ProductDetailBffResponse>>> Get(
        [FromRoute] Guid productId,
        CancellationToken ct)
    {
        var requestId = HttpContext.TraceIdentifier;

        string productBase;
        string reviewBase;
        try
        {
            productBase = await _serviceResolver
                .ResolveBaseAddressAsync(ProductServiceName, ct).ConfigureAwait(false);
            reviewBase = await _serviceResolver
                .ResolveBaseAddressAsync(ReviewServiceName, ct).ConfigureAwait(false);
        }
        catch (BffServiceUnavailableException ex)
        {
            _logger.LogError(
                ex,
                "BFF ProductDetail 下游寻址失败：service={ServiceName}, consul={ConsulServiceName}, requestId={RequestId}",
                ex.ServiceName, ex.ConsulServiceName, requestId);
            return BffErrorEnvelope.DownstreamUnavailable();
        }

        var requests = new BffDownstreamRequest[]
        {
            new()
            {
                Source = ProductDetailSource,
                ServiceUrl = $"{productBase}/api/products/{productId:D}"
            },
            new()
            {
                Source = ReviewSummarySource,
                ServiceUrl = $"{reviewBase}/api/products/{productId:D}/reviews"
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

        // 全部下游失败时返回网关统一 503 信封，不返回 200 + 全 null 的空对象
        if (!response.Success && !response.Partial)
        {
            _logger.LogError(
                "BFF ProductDetail 全部下游失败：productId={ProductId}, requestId={RequestId}, errors={Errors}",
                productId, requestId, string.Join("; ", response.Errors.Select(e => $"{e.Source}:{e.Message}")));
            return BffErrorEnvelope.DownstreamUnavailable();
        }

        return Ok(response);
    }
}
