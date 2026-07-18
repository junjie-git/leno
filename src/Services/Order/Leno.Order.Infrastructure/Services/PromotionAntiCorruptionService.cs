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
/// 促销域防腐层服务，通过 HTTP 调用促销域内部 API 计算订单可享优惠总金额、锁定/释放优惠券。
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
                AntiCorruptionMetrics.RecordFailure("promotion", "calculate_discount");
                throw new OrderDomainException(
                    $"促销域计算优惠失败，状态码 {(int)response.StatusCode}",
                    "ORDER_PROMOTION_CALCULATE_FAILED");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            var payload = await JsonSerializer.DeserializeAsync<ApiResponse<DiscountResultResponse>>(stream, JsonOptions, ct);
            if (payload is null || payload.Data is null)
            {
                _logger.LogWarning("促销域计算优惠返回空数据 UserId={UserId}", userId);
                AntiCorruptionMetrics.RecordFailure("promotion", "calculate_discount");
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
            _logger.LogError(ex, "促销域计算优惠异常 UserId={UserId} Service={Service} Operation={Operation}",
                userId, "promotion", "calculate_discount");
            AntiCorruptionMetrics.RecordFailure("promotion", "calculate_discount");
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
                AntiCorruptionMetrics.RecordFailure("promotion", "release_coupons");
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
            _logger.LogError(ex, "促销域释放优惠券异常 OrderId={OrderId} Service={Service} Operation={Operation}",
                orderId, "promotion", "release_coupons");
            AntiCorruptionMetrics.RecordFailure("promotion", "release_coupons");
            throw new OrderDomainException(
                $"促销域释放优惠券失败：{ex.Message}",
                ex,
                "ORDER_PROMOTION_RELEASE_COUPONS_FAILED");
        }
    }

    /// <inheritdoc />
    public async Task LockCouponAsync(Guid userId, Guid couponId, Guid orderId, CancellationToken ct = default)
    {
        try
        {
            var request = new { userId = userId, couponId = couponId, orderId = orderId };
            var json = JsonSerializer.Serialize(request, JsonOptions);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await _httpClient.PostAsync("internal/promotions/lock-coupon", content, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("促销域锁定优惠券失败 UserId={UserId} CouponId={CouponId} OrderId={OrderId} Status={Status}", userId, couponId, orderId, (int)response.StatusCode);
                AntiCorruptionMetrics.RecordFailure("promotion", "lock_coupon");
                throw new OrderDomainException(
                    $"促销域锁定优惠券失败，状态码 {(int)response.StatusCode}",
                    "ORDER_PROMOTION_LOCK_COUPON_FAILED");
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
            _logger.LogError(ex, "促销域锁定优惠券异常 UserId={UserId} CouponId={CouponId} OrderId={OrderId} Service={Service} Operation={Operation}",
                userId, couponId, orderId, "promotion", "lock_coupon");
            AntiCorruptionMetrics.RecordFailure("promotion", "lock_coupon");
            throw new OrderDomainException(
                $"促销域锁定优惠券失败：{ex.Message}",
                ex,
                "ORDER_PROMOTION_LOCK_COUPON_FAILED");
        }
    }

    private sealed class DiscountResultResponse
    {
        public decimal TotalDiscountAmount { get; set; }

        public string Currency { get; set; } = string.Empty;
    }
}
