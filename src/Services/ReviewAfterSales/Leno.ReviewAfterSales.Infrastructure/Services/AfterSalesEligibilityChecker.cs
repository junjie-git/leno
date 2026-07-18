using System.Text.Json;
using Leno.Infrastructure.AntiCorruption;
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
/// <c>GET internal/v1/orders/{orderId}/status</c> 校验售后期限内、同订单行无进行中同类型售后单且申请人为订单买家。
/// 继承 <see cref="AntiCorruptionBase"/>，远程失败统一抛 <see cref="AntiCorruptionException"/>；业务校验失败抛 <see cref="ReviewDomainException"/>。
/// M5.2：通过 <see cref="AntiCorruptionOptions.TargetInternalApiKeys"/> 读取目标 BC（Order）的 InternalApiKey，
/// 注入 <c>X-Internal-Key</c> 请求头，替代旧的共用 InternalAuth:ApiKey。
/// </summary>
public sealed class AfterSalesEligibilityChecker : AntiCorruptionBase, IAfterSalesEligibilityChecker
{
    private const string InternalKeyName = "X-Internal-Key";
    private const string TargetBc = "Order";
    private const int AfterSalesWindowDays = 15;
    private const int OrderStatusShipped = 2;
    private const int OrderStatusCompleted = 3;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly IAfterSalesRepository _afterSalesRepository;
    private readonly ILogger<AfterSalesEligibilityChecker> _logger;
    private readonly string _targetInternalKey;

    protected override string ServiceName => "order";

    public AfterSalesEligibilityChecker(
        HttpClient httpClient,
        IOptions<AntiCorruptionOptions> options,
        IAfterSalesRepository afterSalesRepository,
        ILogger<AfterSalesEligibilityChecker> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(afterSalesRepository);
        ArgumentNullException.ThrowIfNull(logger);
        _httpClient = httpClient;
        _targetInternalKey = ResolveTargetInternalKey(options);
        _afterSalesRepository = afterSalesRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task EnsureEligibleAsync(Guid orderId, Guid? orderLineId, Guid userId, AfterSalesType type, CancellationToken ct = default)
    {
        var order = await GetOrderStatusAsync(orderId, ct);

        if (order.UserId != userId)
        {
            throw new ReviewDomainException("无权操作此订单", "AFTERSALES_FORBIDDEN");
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

    private Task<OrderStatusResponse> GetOrderStatusAsync(Guid orderId, CancellationToken ct)
        => ExecuteAsync("get_order_status", async token =>
        {
            using var request = CreateRequest($"internal/v1/orders/{orderId}/status");
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            EnsureSuccessStatusCode(response, "get_order_status");

            await using var stream = await response.Content.ReadAsStreamAsync(token);
            var apiResponse = await JsonSerializer.DeserializeAsync<ApiResponse<OrderStatusResponse>>(stream, JsonOptions, token);
            return apiResponse?.Data ?? throw new AntiCorruptionException(
                $"订单域返回空订单状态（orderId={orderId}）",
                "ORDER_REMOTE_FAILED");
        }, ct);

    private HttpRequestMessage CreateRequest(string relativeUri)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, relativeUri);
        request.Headers.TryAddWithoutValidation(InternalKeyName, _targetInternalKey);
        return request;
    }

    private static string ResolveTargetInternalKey(IOptions<AntiCorruptionOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!options.Value.TargetInternalApiKeys.TryGetValue(TargetBc, out var key) || string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException(
                $"AntiCorruption:TargetInternalApiKeys:{TargetBc} 配置缺失，请通过 Consul KV 配置 leno/security/internal-key/{TargetBc}");
        }

        return key;
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
