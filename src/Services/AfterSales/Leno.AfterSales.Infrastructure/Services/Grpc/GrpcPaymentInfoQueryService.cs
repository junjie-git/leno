using Grpc.Core;
using Leno.Infrastructure.AntiCorruption;
using Leno.AfterSales.Application.Services;
using Leno.SharedContracts.Grpc.Payment.V1;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.AfterSales.Infrastructure.Services.Grpc;

/// <summary>
/// 支付信息查询 gRPC 防腐层客户端（售后 BC 独立维护，M4 双轨方案）。
/// 实现 <see cref="IPaymentInfoQueryService"/>，与 <see cref="PaymentInfoQueryService"/>（HttpClient）双轨。
/// 由 <see cref="AntiCorruptionDispatcher{TService}"/> 在运行时选择使用本类或 HttpClient 实现。
/// </summary>
public sealed class GrpcPaymentInfoQueryService
    : GrpcAntiCorruptionClientBase, IPaymentInfoQueryService
{
    private const string TargetBc = "Payment";
    private const string InternalKeyHeader = "x-internal-key";

    private readonly PaymentInternalService.PaymentInternalServiceClient _client;
    private readonly IOptionsMonitor<AntiCorruptionOptions> _options;

    protected override string ServiceName => "payment";

    public GrpcPaymentInfoQueryService(
        PaymentInternalService.PaymentInternalServiceClient client,
        IOptionsMonitor<AntiCorruptionOptions> options,
        ILogger<GrpcPaymentInfoQueryService> logger,
        IServiceProvider? serviceProvider = null)
        : base(serviceProvider, logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public Task<PaymentInfoResult?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default)
        => ExecuteAsync("get_payment_info", async token =>
        {
            var request = new GetPaymentInfoRequest { OrderId = orderId.ToString() };
            var metadata = BuildMetadata();
            var response = await _client.GetPaymentInfoAsync(request, metadata, cancellationToken: token)
                .ConfigureAwait(false);
            return (PaymentInfoResult?)MapToResult(response);
        }, ct);

    private Metadata BuildMetadata()
    {
        var metadata = new Metadata();
        var currentOptions = _options.CurrentValue;
        if (currentOptions.TargetInternalApiKeys.TryGetValue(TargetBc, out var key) && !string.IsNullOrEmpty(key))
        {
            metadata.Add(InternalKeyHeader, key);
        }
        return metadata;
    }

    private static PaymentInfoResult MapToResult(PaymentInfo proto)
    {
        // proto PaymentInfo.channel 为 string（如 "WeChatPay"/"Alipay"），DTO 期望 string
        // 直接使用 proto.Channel（若为空则默认空字符串）
        var channel = proto.HasChannel ? proto.Channel : string.Empty;
        return new PaymentInfoResult
        {
            PaymentId = Guid.TryParse(proto.PaymentId, out var pid) ? pid : Guid.Empty,
            Channel = channel
        };
    }
}
