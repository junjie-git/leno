using Leno.Infrastructure.Auth;
using Leno.Order.Application.Services;
using Leno.SharedContracts.Responses;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Leno.Order.Infrastructure.Services;

/// <summary>
/// 商品域防腐层服务，通过 HTTP 调用商品域内部 API 查询 SKU 当前信息用于构建商品快照与库存校验。
/// 失败时返回 null，由应用层校验抛出异常。
/// </summary>
public sealed class ProductAntiCorruptionService : IProductAntiCorruptionService
{
    private const string InternalKeyName = "X-Internal-Key";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly ILogger<ProductAntiCorruptionService> _logger;

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
    public async Task<SkuInfo?> GetSkuInfoAsync(Guid skuId, CancellationToken ct = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync($"internal/products/skus/{skuId}", ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("商品域查询 SKU 失败 SkuId={SkuId} Status={Status}", skuId, (int)response.StatusCode);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            var payload = await JsonSerializer.DeserializeAsync<ApiResponse<SkuInfoResponse>>(stream, JsonOptions, ct);
            if (payload is null || payload.Data is null)
            {
                _logger.LogWarning("商品域查询 SKU 返回空数据 SkuId={SkuId}", skuId);
                return null;
            }

            var d = payload.Data;
            return new SkuInfo
            {
                SkuId = d.SkuId,
                SellerId = d.SellerId,
                ProductName = d.Title,
                SkuName = d.Title,
                MainImage = d.MainImageUrl,
                UnitPrice = d.Price,
                IsOnSale = d.Available
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "商品域查询 SKU 异常 SkuId={SkuId}", skuId);
            return null;
        }
    }

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
