using System.Net.Http.Json;
using System.Text.Json;
using Leno.Cart.Domain.Exceptions;
using Leno.Cart.Domain.Services;
using Leno.Infrastructure.Auth;
using Leno.SharedContracts.Responses;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.Cart.Infrastructure.Services;

/// <summary>
/// 购物车价格防腐层实现。
/// 通过商品域内部 API（POST internal/products/skus/batch）批量查询 SKU 价格与可售状态，
/// 使用 X-Internal-Key 头部鉴权。
/// 调用失败（非 2xx / 网络异常）时抛出 <see cref="CartDomainException"/>，由应用层决定降级或阻止用例，
/// 不再静默返回空集合掩盖故障，以避免购物车出现 0 元可结算的误导。
/// </summary>
public sealed class CartPriceService : ICartPriceService
{
    private const string BatchEndpoint = "internal/products/skus/batch";
    private const string InternalKeyHeader = "X-Internal-Key";
    private const string PriceUnavailableErrorCode = "CART_PRICE_UNAVAILABLE";
    private const string PriceUnavailableMessage = "商品价格服务暂时不可用";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly InternalApiKeyOptions _internalKeyOptions;
    private readonly ILogger<CartPriceService> _logger;

    public CartPriceService(
        HttpClient httpClient,
        IOptions<InternalApiKeyOptions> internalKeyOptions,
        ILogger<CartPriceService> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(internalKeyOptions);
        ArgumentNullException.ThrowIfNull(logger);
        _httpClient = httpClient;
        _internalKeyOptions = internalKeyOptions.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SkuPriceSnapshot>> GetSkuPricesAsync(IEnumerable<Guid> skuIds, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(skuIds);
        var ids = skuIds.ToList();

        if (ids.Count == 0)
        {
            return Array.Empty<SkuPriceSnapshot>();
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, BatchEndpoint)
            {
                Content = JsonContent.Create(ids, options: JsonOptions)
            };
            request.Headers.TryAddWithoutValidation(InternalKeyHeader, _internalKeyOptions.ApiKey);

            using var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("商品域 SKU 批量查询失败 StatusCode={StatusCode} Count={Count}",
                    (int)response.StatusCode, ids.Count);
                throw new CartDomainException(PriceUnavailableMessage, PriceUnavailableErrorCode, 503);
            }

            var apiResponse = await response.Content
                .ReadFromJsonAsync<ApiResponse<List<SkuInfoResponse>>>(JsonOptions, ct);
            if (apiResponse?.Data is null || apiResponse.Data.Count == 0)
            {
                return Array.Empty<SkuPriceSnapshot>();
            }

            return apiResponse.Data.Select(MapToSnapshot).ToList();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (CartDomainException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "商品域 SKU 批量查询异常 Count={Count}", ids.Count);
            throw new CartDomainException(PriceUnavailableMessage, PriceUnavailableErrorCode, 503);
        }
    }

    private static SkuPriceSnapshot MapToSnapshot(SkuInfoResponse dto) => new()
    {
        SkuId = dto.SkuId,
        Price = dto.Price,
        Currency = string.IsNullOrEmpty(dto.Currency) ? "CNY" : dto.Currency,
        Available = dto.Available,
        Title = dto.Title,
        MainImageUrl = dto.MainImageUrl,
        SellerId = dto.SellerId
    };

    /// <summary>商品域 SKU 概要信息反序列化 DTO（避免直接依赖商品域 Application 层）。</summary>
    private sealed class SkuInfoResponse
    {
        public Guid SkuId { get; set; }

        public decimal Price { get; set; }

        public string Currency { get; set; } = "CNY";

        public bool Available { get; set; }

        public string Title { get; set; } = string.Empty;

        public string MainImageUrl { get; set; } = string.Empty;

        public Guid SellerId { get; set; }
    }
}
