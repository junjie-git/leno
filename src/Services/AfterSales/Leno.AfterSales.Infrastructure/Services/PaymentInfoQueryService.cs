using System.Globalization;
using System.Text.Json;
using Leno.Infrastructure.AntiCorruption;
using Leno.AfterSales.Application.Services;
using Leno.SharedContracts.Responses;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.AfterSales.Infrastructure.Services;

/// <summary>
/// 支付信息查询防腐层实现（售后 BC 独立维护），通过 HTTP 调用支付域内部接口
/// <c>GET internal/v1/payments/{orderId}/info</c> 获取支付单标识与渠道。
/// 继承 <see cref="AntiCorruptionBase"/>，远程失败统一抛 <see cref="AntiCorruptionException"/>，不再返回 null。
/// M5.2：通过 <see cref="AntiCorruptionOptions.TargetInternalApiKeys"/> 读取目标 BC（Payment）的 InternalApiKey，
/// 注入 <c>X-Internal-Key</c> 请求头，替代旧的共用 InternalAuth:ApiKey。
/// </summary>
public sealed class PaymentInfoQueryService : AntiCorruptionBase, IPaymentInfoQueryService
{
    private const string InternalKeyName = "X-Internal-Key";
    private const string TargetBc = "Payment";
    private const string ChannelWeChatPay = "WeChatPay";
    private const string ChannelAlipay = "Alipay";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly ILogger<PaymentInfoQueryService> _logger;
    private readonly string _targetInternalKey;

    protected override string ServiceName => "payment";

    public PaymentInfoQueryService(
        HttpClient httpClient,
        IOptions<AntiCorruptionOptions> options,
        ILogger<PaymentInfoQueryService> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _httpClient = httpClient;
        _targetInternalKey = ResolveTargetInternalKey(options);
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
        request.Headers.TryAddWithoutValidation(InternalKeyName, _targetInternalKey);
        return request;
    }

    private static string MapChannel(int channel) => channel switch
    {
        0 => ChannelWeChatPay,
        1 => ChannelAlipay,
        _ => channel.ToString(CultureInfo.InvariantCulture)
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

    private sealed class PaymentInfoResponse
    {
        public Guid PaymentId { get; set; }

        public int Channel { get; set; }

        public Guid OrderId { get; set; }

        public int Status { get; set; }
    }
}
