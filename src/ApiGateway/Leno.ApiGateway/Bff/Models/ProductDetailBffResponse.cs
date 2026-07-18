using System.Text.Json;

namespace Leno.ApiGateway.Bff.Models;

/// <summary>
/// 商品详情聚合响应：商品主体 + 评论摘要。
/// </summary>
public sealed class ProductDetailBffResponse
{
    /// <summary>商品主体（来自 Product BC <c>/api/products/{productId}</c>）。</summary>
    public JsonElement? Product { get; init; }

    /// <summary>评论摘要（来自 ReviewAfterSales BC <c>/api/reviews?spuId={productId}</c>）。</summary>
    public JsonElement? ReviewSummary { get; init; }
}
