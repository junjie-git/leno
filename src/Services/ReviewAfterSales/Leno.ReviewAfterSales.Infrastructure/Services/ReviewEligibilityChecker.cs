using System.Text.Json;
using Leno.Infrastructure.Auth;
using Leno.ReviewAfterSales.Domain.Exceptions;
using Leno.ReviewAfterSales.Domain.Repositories;
using Leno.ReviewAfterSales.Domain.Services;
using Leno.SharedContracts.Responses;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.ReviewAfterSales.Infrastructure.Services;

/// <summary>
/// 评价资格校验器防腐层实现，通过 HTTP 调用订单域内部接口
/// <c>GET internal/orders/{orderId}/status</c> 校验订单已完成、订单行未重复评价、在评价期限内且申请人为订单买家。
/// 校验失败抛出 <see cref="ReviewDomainException"/>。
/// </summary>
public sealed class ReviewEligibilityChecker : IReviewEligibilityChecker
{
    private const string InternalKeyName = "X-Internal-Key";
    private const int ReviewWindowDays = 30;
    private const int OrderStatusCompleted = 3;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly InternalApiKeyOptions _authOptions;
    private readonly IReviewRepository _reviewRepository;
    private readonly ILogger<ReviewEligibilityChecker> _logger;

    public ReviewEligibilityChecker(
        HttpClient httpClient,
        IOptions<InternalApiKeyOptions> authOptions,
        IReviewRepository reviewRepository,
        ILogger<ReviewEligibilityChecker> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(authOptions);
        ArgumentNullException.ThrowIfNull(reviewRepository);
        ArgumentNullException.ThrowIfNull(logger);
        _httpClient = httpClient;
        _authOptions = authOptions.Value;
        _reviewRepository = reviewRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task EnsureEligibleAsync(Guid orderId, Guid orderLineId, Guid userId, CancellationToken ct = default)
    {
        var order = await GetOrderStatusAsync(orderId, ct);
        if (order is null)
        {
            throw new ReviewDomainException("订单不存在", "REVIEW_ORDER_NOT_FOUND");
        }

        if (order.UserId != userId)
        {
            throw new ReviewDomainException("无权操作此订单", "REVIEW_FORBIDDEN");
        }

        if (order.Status != OrderStatusCompleted)
        {
            throw new ReviewDomainException("订单未完成，不可评价", "REVIEW_ORDER_NOT_COMPLETED");
        }

        if (order.CompletedAt != default
            && DateTime.UtcNow - order.CompletedAt > TimeSpan.FromDays(ReviewWindowDays))
        {
            throw new ReviewDomainException("评价已超过期限", "REVIEW_WINDOW_EXPIRED");
        }

        var exists = await _reviewRepository.ExistsByOrderLineAsync(orderLineId, ct);
        if (exists)
        {
            throw new ReviewDomainException("该订单行已评价", "REVIEW_DUPLICATE");
        }
    }

    private async Task<OrderStatusResponse?> GetOrderStatusAsync(Guid orderId, CancellationToken ct)
    {
        using var request = CreateRequest($"internal/orders/{orderId}/status");
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("订单状态查询失败 OrderId={OrderId} Status={Status}", orderId, response.StatusCode);
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var apiResponse = await JsonSerializer.DeserializeAsync<ApiResponse<OrderStatusResponse>>(stream, JsonOptions, ct);
        return apiResponse?.Data;
    }

    private HttpRequestMessage CreateRequest(string relativeUri)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, relativeUri);
        if (!string.IsNullOrEmpty(_authOptions.ApiKey))
        {
            request.Headers.TryAddWithoutValidation(InternalKeyName, _authOptions.ApiKey);
        }

        return request;
    }

    private sealed class OrderStatusResponse
    {
        public Guid OrderId { get; set; }

        public int Status { get; set; }

        public Guid UserId { get; set; }

        public DateTime CompletedAt { get; set; }

        public DateTime CreatedAt { get; set; }

        public List<OrderItemStatusResponse> Items { get; set; } = [];
    }

    private sealed class OrderItemStatusResponse
    {
        public Guid OrderLineId { get; set; }

        public Guid SkuId { get; set; }

        public int Quantity { get; set; }

        public int AfterSalesStatus { get; set; }
    }
}
