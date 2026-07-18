using Leno.Infrastructure.AntiCorruption;
using Leno.Infrastructure.Auth;
using Leno.Order.Application.Services;
using Leno.SharedContracts.Responses;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Leno.Order.Infrastructure.Services;

/// <summary>
/// 商品域防腐层服务，通过 HTTP 调用商品域内部 API 查询 SKU 当前信息用于构建商品快照与库存校验。
/// 继承 <see cref="AntiCorruptionBase"/>，远程失败统一抛 <see cref="AntiCorruptionException"/>，不再返回 null。
/// </summary>
public sealed class ProductAntiCorruptionService : AntiCorruptionBase, IProductAntiCorruptionService
{
    private const string InternalKeyName = "X-Internal-Key";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly ILogger<ProductAntiCorruptionService> _logger;

    protected override string ServiceName => "product";

    public ProductAntiCorruptionService(
        HttpClient httpClient,
        IOptions<InternalApiKeyOptions> options,
        ILogger<ProductAntiCorruptionService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        ApplyInternalKey(httpClient, options);
    }

    /// <inheritdoc />
    public Task<SkuInfo?> GetSkuInfoAsync(Guid skuId, CancellationToken ct = default)
        => ExecuteAsync("get_sku_info", async token =>
        {
            using var response = await _httpClient.GetAsync($"internal/v1/products/skus/{skuId}", token);
            EnsureSuccessStatusCode(response, "get_sku_info");

            await using var stream = await response.Content.ReadAsStreamAsync(token);
            var payload = await JsonSerializer.DeserializeAsync<ApiResponse<SkuInfoResponse>>(stream, JsonOptions, token);
            if (payload is null || payload.Data is null)
            {
                throw new AntiCorruptionException(
                    $"商品域返回空 SKU 信息（skuId={skuId}）",
                    "PRODUCT_REMOTE_FAILED");
            }

            var d = payload.Data;
            return (SkuInfo?)new SkuInfo
            {
                SkuId = d.SkuId,
                SellerId = d.SellerId,
                ProductName = d.Title,
                SkuName = d.Title,
                MainImage = d.MainImageUrl,
                UnitPrice = d.Price,
                IsOnSale = d.Available
            };
        }, ct);

    private static void ApplyInternalKey(HttpClient httpClient, IOptions<InternalApiKeyOptions> options)
    {
        var apiKey = options.Value.ApiKey;
        if (!string.IsNullOrEmpty(apiKey))
        {
            httpClient.DefaultRequestHeaders.Add(InternalKeyName, apiKey);
        }
    }

    private sealed class SkuInfoResponse
    {
        public Guid SkuId { get; set; }

        public decimal Price { get; set; }

        public string Currency { get; set; } = string.Empty;

        public bool Available { get; set; }

        public string Title { get; set; } = string.Empty;

        public string MainImageUrl { get; set; } = string.Empty;

        public Guid SellerId { get; set; }
    }
}
