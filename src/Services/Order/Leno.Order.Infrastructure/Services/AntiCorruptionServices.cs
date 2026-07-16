using Leno.Infrastructure.Auth;
using Leno.Order.Application.Services;
using Leno.Order.Domain.Exceptions;
using Leno.SharedContracts.Responses;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace Leno.Order.Infrastructure.Services;

/// <summary>
/// 商品域防腐层实现，通过 HTTP 调用商品域内部 API 查询 SKU 当前信息用于构建商品快照与库存校验。
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

/// <summary>
/// 促销域防腐层实现，通过 HTTP 调用促销域内部 API 计算订单可享优惠总金额。
/// 远程失败（网络异常、非 2xx、超时）抛 <see cref="OrderDomainException"/>，由应用层处理；用户取消透传 <see cref="OperationCanceledException"/>。
/// </summary>
public sealed class PromotionAntiCorruptionService : IPromotionAntiCorruptionService
{
    private const string InternalKeyName = "X-Internal-Key";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly ILogger<PromotionAntiCorruptionService> _logger;

    public PromotionAntiCorruptionService(
        HttpClient httpClient,
        IOptions<InternalApiKeyOptions> options,
        ILogger<PromotionAntiCorruptionService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        ApplyInternalKey(httpClient, options);
    }

    /// <inheritdoc />
    public async Task<decimal> CalculateDiscountAsync(
        Guid userId,
        List<(Guid SkuId, decimal Subtotal)> items,
        CancellationToken ct = default)
    {
        try
        {
            var request = new
            {
                userId = userId,
                items = items.Select(i => new { skuId = i.SkuId, subtotal = i.Subtotal }).ToArray()
            };
            var json = JsonSerializer.Serialize(request, JsonOptions);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await _httpClient.PostAsync("internal/promotions/calculate", content, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("促销域计算优惠失败 UserId={UserId} Status={Status}", userId, (int)response.StatusCode);
                throw new OrderDomainException(
                    $"促销域计算优惠失败，状态码 {(int)response.StatusCode}",
                    "ORDER_PROMOTION_CALCULATE_FAILED");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            var payload = await JsonSerializer.DeserializeAsync<ApiResponse<DiscountResultResponse>>(stream, JsonOptions, ct);
            if (payload is null || payload.Data is null)
            {
                _logger.LogWarning("促销域计算优惠返回空数据 UserId={UserId}", userId);
                throw new OrderDomainException(
                    "促销域计算优惠返回空数据",
                    "ORDER_PROMOTION_CALCULATE_FAILED");
            }

            return payload.Data.TotalDiscountAmount;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OrderDomainException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "促销域计算优惠异常 UserId={UserId}", userId);
            throw new OrderDomainException(
                $"促销域计算优惠失败：{ex.Message}",
                ex,
                "ORDER_PROMOTION_CALCULATE_FAILED");
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

    /// <inheritdoc />
    public async Task ReleaseCouponsAsync(Guid orderId, CancellationToken ct = default)
    {
        try
        {
            var request = new { orderId = orderId };
            var json = JsonSerializer.Serialize(request, JsonOptions);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await _httpClient.PostAsync("internal/promotions/release-coupons", content, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("促销域释放优惠券失败 OrderId={OrderId} Status={Status}", orderId, (int)response.StatusCode);
                throw new OrderDomainException(
                    $"促销域释放优惠券失败，状态码 {(int)response.StatusCode}",
                    "ORDER_PROMOTION_RELEASE_COUPONS_FAILED");
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OrderDomainException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "促销域释放优惠券异常 OrderId={OrderId}", orderId);
            throw new OrderDomainException(
                $"促销域释放优惠券失败：{ex.Message}",
                ex,
                "ORDER_PROMOTION_RELEASE_COUPONS_FAILED");
        }
    }

    private sealed class DiscountResultResponse
    {
        public decimal TotalDiscountAmount { get; set; }

        public string Currency { get; set; } = string.Empty;
    }
}

/// <summary>
/// 积分域防腐层实现，通过 HTTP 调用积分域内部 API 试算/冻结/释放积分。
/// TryOffsetAsync 返回实际可抵金额（失败返回 0，预览降级）；Freeze/ConfirmDeduction/Release 远程失败（网络异常、非 2xx、超时）抛 <see cref="OrderDomainException"/>，用户取消透传 <see cref="OperationCanceledException"/>。
/// </summary>
public sealed class PointsAntiCorruptionService : IPointsAntiCorruptionService
{
    private const string InternalKeyName = "X-Internal-Key";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly ILogger<PointsAntiCorruptionService> _logger;

    public PointsAntiCorruptionService(
        HttpClient httpClient,
        IOptions<InternalApiKeyOptions> options,
        ILogger<PointsAntiCorruptionService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        ApplyInternalKey(httpClient, options);
    }

    /// <inheritdoc />
    public async Task<decimal> TryOffsetAsync(Guid userId, int pointsToUse, CancellationToken ct = default)
    {
        try
        {
            var request = new { userId = userId, pointsToUse = pointsToUse };
            var json = JsonSerializer.Serialize(request, JsonOptions);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await _httpClient.PostAsync("internal/points/trial-offset", content, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("积分域试算抵现失败 UserId={UserId} Status={Status}", userId, (int)response.StatusCode);
                return 0m;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            var payload = await JsonSerializer.DeserializeAsync<ApiResponse<TrialOffsetResponse>>(stream, JsonOptions, ct);
            if (payload is null || payload.Data is null)
            {
                _logger.LogWarning("积分域试算抵现返回空数据 UserId={UserId}", userId);
                return 0m;
            }

            return payload.Data.OffsetAmount;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "积分域试算抵现异常 UserId={UserId}", userId);
            return 0m;
        }
    }

    /// <inheritdoc />
    public async Task FreezeAsync(Guid userId, Guid orderId, int pointsToUse, CancellationToken ct = default)
    {
        try
        {
            var request = new { userId = userId, orderId = orderId, pointsToUse = pointsToUse };
            var json = JsonSerializer.Serialize(request, JsonOptions);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await _httpClient.PostAsync("internal/points/freeze", content, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("积分域冻结失败 OrderId={OrderId} Status={Status}", orderId, (int)response.StatusCode);
                throw new OrderDomainException(
                    $"积分域冻结失败，状态码 {(int)response.StatusCode}",
                    "ORDER_POINTS_FREEZE_FAILED");
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OrderDomainException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "积分域冻结异常 OrderId={OrderId}", orderId);
            throw new OrderDomainException(
                $"积分域冻结失败：{ex.Message}",
                ex,
                "ORDER_POINTS_FREEZE_FAILED");
        }
    }

    /// <inheritdoc />
    public async Task ReleaseAsync(Guid orderId, CancellationToken ct = default)
    {
        try
        {
            var request = new { orderId = orderId };
            var json = JsonSerializer.Serialize(request, JsonOptions);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await _httpClient.PostAsync("internal/points/release", content, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("积分域释放失败 OrderId={OrderId} Status={Status}", orderId, (int)response.StatusCode);
                throw new OrderDomainException(
                    $"积分域释放失败，状态码 {(int)response.StatusCode}",
                    "ORDER_POINTS_RELEASE_FAILED");
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OrderDomainException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "积分域释放异常 OrderId={OrderId}", orderId);
            throw new OrderDomainException(
                $"积分域释放失败：{ex.Message}",
                ex,
                "ORDER_POINTS_RELEASE_FAILED");
        }
    }

    /// <inheritdoc />
    public async Task ConfirmDeductionAsync(Guid orderId, CancellationToken ct = default)
    {
        try
        {
            var request = new { orderId = orderId };
            var json = JsonSerializer.Serialize(request, JsonOptions);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await _httpClient.PostAsync("internal/points/confirm", content, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("积分域确认扣减失败 OrderId={OrderId} Status={Status}", orderId, (int)response.StatusCode);
                throw new OrderDomainException(
                    $"积分域确认扣减失败，状态码 {(int)response.StatusCode}",
                    "ORDER_POINTS_CONFIRM_FAILED");
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OrderDomainException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "积分域确认扣减异常 OrderId={OrderId}", orderId);
            throw new OrderDomainException(
                $"积分域确认扣减失败：{ex.Message}",
                ex,
                "ORDER_POINTS_CONFIRM_FAILED");
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

    private sealed class TrialOffsetResponse
    {
        public decimal OffsetAmount { get; set; }

        public string Currency { get; set; } = string.Empty;
    }
}
