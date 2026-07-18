using System.Globalization;
using System.Text.Json;
using Leno.Infrastructure.AntiCorruption;
using Leno.Infrastructure.Auth;
using Leno.ReviewAfterSales.Application.Services;
using Leno.SharedContracts.Responses;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.ReviewAfterSales.Infrastructure.Services;

/// <summary>
/// 支付信息查询防腐层实现，通过 HTTP 调用支付域内部接口
/// <c>GET internal/v1/payments/{orderId}/info</c> 获取支付单标识与渠道。
/// 继承 <see cref="AntiCorruptionBase"/>，远程失败统一抛 <see cref="AntiCorruptionException"/>，不再返回 null。
/// </summary>
public sealed class PaymentInfoQueryService : AntiCorruptionBase, IPaymentInfoQueryService
{
    private const string InternalKeyName = "X-Internal-Key";
    private const string ChannelWeChatPay = "WeChatPay";
    private const string ChannelAlipay = "Alipay";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly InternalApiKeyOptions _authOptions;
    private readonly ILogger<PaymentInfoQueryService> _logger;

    protected override string ServiceName => "payment";

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
    public Task<PaymentInfoResult?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default)
        => ExecuteAsync("get_payment_info", async token =>
        {
            using var request = CreateRequest($"internal/v1/payments/{orderId}/info");
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            EnsureSuccessStatusCode(response, "get_payment_info");

            await using var stream = await response.Content.ReadAsStreamAsync(token);
            var apiResponse = await JsonSerializer.DeserializeAsync<ApiResponse<PaymentInfoResponse>>(stream, JsonOptions, token);
            if (apiResponse?.Data is null)
            {
                throw new AntiCorruptionException(
                    $"支付域返回空支付信息（orderId={orderId}）",
                    "PAYMENT_REMOTE_FAILED");
            }

            var data = apiResponse.Data;
            return (PaymentInfoResult?)new PaymentInfoResult
            {
                PaymentId = data.PaymentId,
                Channel = MapChannel(data.Channel)
            };
        }, ct);

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
