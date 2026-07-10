using System.Text.Json;
using Leno.Infrastructure.Auth;
using Leno.ReviewAfterSales.Domain.Exceptions;
using Leno.ReviewAfterSales.Domain.Repositories;
using Leno.ReviewAfterSales.Domain.Services;
using Leno.ReviewAfterSales.Domain.ValueObjects;
using Leno.SharedContracts.Responses;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.ReviewAfterSales.Infrastructure.Services;

/// <summary>
/// 售后资格校验器防腐层实现，通过 HTTP 调用订单域内部接口
/// <c>GET internal/orders/{orderId}/status</c> 校验售后期限内、同订单行无进行中同类型售后单且申请人为订单买家。
/// 校验失败抛出 <see cref="ReviewDomainException"/>。
/// </summary>
public sealed class AfterSalesEligibilityChecker : IAfterSalesEligibilityChecker
{
    private const string InternalKeyName = "X-Internal-Key";
    private const int AfterSalesWindowDays = 15;
    private const int OrderStatusShipped = 2;
    private const int OrderStatusCompleted = 3;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly InternalApiKeyOptions _authOptions;
    private readonly IAfterSalesRepository _afterSalesRepository;
    private readonly ILogger<AfterSalesEligibilityChecker> _logger;

    public AfterSalesEligibilityChecker(
        HttpClient httpClient,
        IOptions<InternalApiKeyOptions> authOptions,
        IAfterSalesRepository afterSalesRepository,
        ILogger<AfterSalesEligibilityChecker> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(authOptions);
        ArgumentNullException.ThrowIfNull(afterSalesRepository);
        ArgumentNullException.ThrowIfNull(logger);
        _httpClient = httpClient;
        _authOptions = authOptions.Value;
        _afterSalesRepository = afterSalesRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task EnsureEligibleAsync(Guid orderId, Guid? orderLineId, Guid userId, AfterSalesType type, CancellationToken ct = default)
    {
        var order = await GetOrderStatusAsync(orderId, ct);
        if (order is null)
        {
            throw new ReviewDomainException("订单不存在", "AFTERSALES_ORDER_NOT_FOUND", 404);
        }

        if (order.UserId != userId)
        {
            throw new ReviewDomainException("无权操作此订单", "AFTERSALES_FORBIDDEN", 403);
        }

        if (order.Status != OrderStatusShipped && order.Status != OrderStatusCompleted)
        {
            throw new ReviewDomainException("订单当前状态不支持售后申请", "AFTERSALES_STATUS_INVALID");
        }

        if (order.Status == OrderStatusCompleted
            && order.CompletedAt != default
            && DateTime.UtcNow - order.CompletedAt > TimeSpan.FromDays(AfterSalesWindowDays))
        {
            throw new ReviewDomainException("售后申请已超过期限", "AFTERSALES_WINDOW_EXPIRED");
        }

        if (orderLineId.HasValue)
        {
            var hasActive = await _afterSalesRepository.HasActiveByOrderLineAsync(orderLineId.Value, type, ct);
            if (hasActive)
            {
                throw new ReviewDomainException("该订单行已存在进行中的同类型售后单", "AFTERSALES_DUPLICATE");
            }
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
