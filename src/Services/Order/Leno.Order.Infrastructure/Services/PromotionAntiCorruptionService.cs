using Leno.Infrastructure.AntiCorruption;
using Leno.Infrastructure.Auth;
using Leno.Order.Application.Services;
using Leno.SharedContracts.Responses;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace Leno.Order.Infrastructure.Services;

/// <summary>
/// 促销域防腐层服务，通过 HTTP 调用促销域内部 API 计算订单可享优惠总金额、锁定/释放优惠券。
/// 继承 <see cref="AntiCorruptionBase"/>，远程失败统一抛 <see cref="AntiCorruptionException"/>，由应用层处理；用户取消透传 <see cref="OperationCanceledException"/>。
/// </summary>
public sealed class PromotionAntiCorruptionService : AntiCorruptionBase, IPromotionAntiCorruptionService
{
    private const string InternalKeyName = "X-Internal-Key";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly ILogger<PromotionAntiCorruptionService> _logger;

    protected override string ServiceName => "promotion";

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
    public Task<decimal> CalculateDiscountAsync(
        Guid userId,
        List<(Guid SkuId, decimal Subtotal)> items,
        CancellationToken ct = default)
        => ExecuteAsync("calculate_discount", async token =>
        {
            var request = new
            {
                userId = userId,
                items = items.Select(i => new { skuId = i.SkuId, subtotal = i.Subtotal }).ToArray()
            };
            var json = JsonSerializer.Serialize(request, JsonOptions);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await _httpClient.PostAsync("internal/v1/promotions/calculate", content, token);
            EnsureSuccessStatusCode(response, "calculate_discount");

            await using var stream = await response.Content.ReadAsStreamAsync(token);
            var payload = await JsonSerializer.DeserializeAsync<ApiResponse<DiscountResultResponse>>(stream, JsonOptions, token);
            if (payload is null || payload.Data is null)
            {
                throw new AntiCorruptionException(
                    $"促销域计算优惠返回空数据（userId={userId}）",
                    "PROMOTION_REMOTE_FAILED");
            }

            return payload.Data.TotalDiscountAmount;
        }, ct);

    /// <inheritdoc />
    public Task ReleaseCouponsAsync(Guid orderId, CancellationToken ct = default)
        => ExecuteAsync("release_coupons", async token =>
        {
            var request = new { orderId = orderId };
            var json = JsonSerializer.Serialize(request, JsonOptions);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await _httpClient.PostAsync("internal/v1/promotions/release-coupons", content, token);
            EnsureSuccessStatusCode(response, "release_coupons");
        }, ct);

    /// <inheritdoc />
    public Task LockCouponAsync(Guid userId, Guid couponId, Guid orderId, CancellationToken ct = default)
        => ExecuteAsync("lock_coupon", async token =>
        {
            var request = new { userId = userId, couponId = couponId, orderId = orderId };
            var json = JsonSerializer.Serialize(request, JsonOptions);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await _httpClient.PostAsync("internal/v1/promotions/lock-coupon", content, token);
            EnsureSuccessStatusCode(response, "lock_coupon");
        }, ct);

    private static void ApplyInternalKey(HttpClient httpClient, IOptions<InternalApiKeyOptions> options)
    {
        var apiKey = options.Value.ApiKey;
        if (!string.IsNullOrEmpty(apiKey))
        {
            httpClient.DefaultRequestHeaders.Add(InternalKeyName, apiKey);
        }
    }

    private sealed class DiscountResultResponse
    {
        public decimal TotalDiscountAmount { get; set; }

        public string Currency { get; set; } = string.Empty;
    }
}
