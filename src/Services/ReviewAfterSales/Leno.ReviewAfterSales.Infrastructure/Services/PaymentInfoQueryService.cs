using System.Globalization;
using System.Text.Json;
using Leno.Infrastructure.Auth;
using Leno.ReviewAfterSales.Application.Services;
using Leno.SharedContracts.Responses;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.ReviewAfterSales.Infrastructure.Services;

/// <summary>
/// 支付信息查询防腐层实现，通过 HTTP 调用支付域内部接口
/// <c>GET internal/payments/{orderId}/info</c> 获取支付单标识与渠道。
/// 调用失败或支付单不存在时返回 null。
/// </summary>
public sealed class PaymentInfoQueryService : IPaymentInfoQueryService
{
    private const string InternalKeyName = "X-Internal-Key";
    private const string ChannelWeChatPay = "WeChatPay";
    private const string ChannelAlipay = "Alipay";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly InternalApiKeyOptions _authOptions;
    private readonly ILogger<PaymentInfoQueryService> _logger;

    public PaymentInfoQueryService(
        HttpClient httpClient,
        IOptions<InternalApiKeyOptions> authOptions,
        ILogger<PaymentInfoQueryService> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(authOptions);
        ArgumentNullException.ThrowIfNull(logger);
        _httpClient = httpClient;
        _authOptions = authOptions.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PaymentInfoResult?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default)
    {
        using var request = CreateRequest($"internal/payments/{orderId}/info");
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("支付信息查询失败 OrderId={OrderId} Status={Status}", orderId, response.StatusCode);
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var apiResponse = await JsonSerializer.DeserializeAsync<ApiResponse<PaymentInfoResponse>>(stream, JsonOptions, ct);

        if (apiResponse?.Data is null)
        {
            return null;
        }

        var data = apiResponse.Data;
        return new PaymentInfoResult
        {
            PaymentId = data.PaymentId,
            Channel = MapChannel(data.Channel)
        };
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

    private static string MapChannel(int channel) => channel switch
    {
        0 => ChannelWeChatPay,
        1 => ChannelAlipay,
        _ => channel.ToString(CultureInfo.InvariantCulture)
    };

    private sealed class PaymentInfoResponse
    {
        public Guid PaymentId { get; set; }

        public int Channel { get; set; }

        public Guid OrderId { get; set; }

        public int Status { get; set; }
    }
}
