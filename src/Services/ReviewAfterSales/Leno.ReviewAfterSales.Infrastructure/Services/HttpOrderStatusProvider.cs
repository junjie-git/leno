using System.Text.Json;
using Leno.Infrastructure.AntiCorruption;
using Leno.ReviewAfterSales.Domain.Services;
using Leno.SharedContracts.Responses;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.ReviewAfterSales.Infrastructure.Services;

/// <summary>
/// 订单状态查询 HttpClient 防腐层实现（M4 双轨方案从 AfterSalesEligibilityChecker/ReviewEligibilityChecker 抽取）。
/// 通过 HTTP 调用订单域内部端点 <c>GET internal/v1/orders/{orderId}/status</c> 获取订单状态。
/// 继承 <see cref="AntiCorruptionBase"/>，远程失败统一抛 <see cref="AntiCorruptionException"/>。
/// </summary>
public sealed class HttpOrderStatusProvider : AntiCorruptionBase, IOrderStatusProvider
{
    private const string InternalKeyName = "X-Internal-Key";
    private const string TargetBc = "Order";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpOrderStatusProvider> _logger;
    private readonly string _targetInternalKey;

    protected override string ServiceName => "order";

    public HttpOrderStatusProvider(
        HttpClient httpClient,
        IOptions<AntiCorruptionOptions> options,
        ILogger<HttpOrderStatusProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _httpClient = httpClient;
        _targetInternalKey = ResolveTargetInternalKey(options);
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<OrderStatusInfo?> GetOrderStatusAsync(Guid orderId, CancellationToken ct = default)
        => ExecuteAsync("get_order_status", async token =>
        {
            using var request = CreateRequest($"internal/v1/orders/{orderId}/status");
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            EnsureSuccessStatusCode(response, "get_order_status");

            await using var stream = await response.Content.ReadAsStreamAsync(token);
            var apiResponse = await JsonSerializer.DeserializeAsync<ApiResponse<OrderStatusResponse>>(stream, JsonOptions, token);
            if (apiResponse?.Data is null)
            {
                throw new AntiCorruptionException(
                    $"订单域返回空订单状态（orderId={orderId}）",
                    "ORDER_REMOTE_FAILED");
            }

            return (OrderStatusInfo?)MapToInfo(apiResponse.Data);
        }, ct);

    private HttpRequestMessage CreateRequest(string relativeUri)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, relativeUri);
        request.Headers.TryAddWithoutValidation(InternalKeyName, _targetInternalKey);
        return request;
    }

    private static OrderStatusInfo MapToInfo(OrderStatusResponse dto) => new()
    {
        OrderId = dto.OrderId,
        Status = dto.Status,
        UserId = dto.UserId,
        SellerId = dto.SellerId,
        CompletedAt = dto.CompletedAt,
        CreatedAt = dto.CreatedAt,
        Items = dto.Items.Select(i => new OrderItemStatusInfo
        {
            OrderLineId = i.OrderLineId,
            SkuId = i.SkuId,
            Quantity = i.Quantity,
            AfterSalesStatus = i.AfterSalesStatus
        }).ToList()
    };

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
        public Guid SellerId { get; set; }
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
