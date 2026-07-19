using System.Globalization;
using Grpc.Core;
using Leno.Payment.Application;
using Leno.SharedContracts.Grpc.Payment.V1;
using Microsoft.AspNetCore.Authorization;

namespace Leno.Payment.Api.GrpcServices;

/// <summary>
/// 支付域 gRPC 服务端（M4 双轨方案）。
/// 复用 <see cref="IPaymentInternalQueryService"/> 业务逻辑，与 InternalPaymentsController HTTP 路径双轨。
/// 鉴权由 GrpcInternalKeyInterceptor 拦截器统一处理（metadata x-internal-key）。
/// </summary>
[Authorize]
public sealed class PaymentGrpcService : PaymentInternalService.PaymentInternalServiceBase
{
    private readonly IPaymentInternalQueryService _queryService;
    private readonly ILogger<PaymentGrpcService> _logger;

    public PaymentGrpcService(
        IPaymentInternalQueryService queryService,
        ILogger<PaymentGrpcService> logger)
    {
        _queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override async Task<PaymentInfo> GetPaymentInfo(
        GetPaymentInfoRequest request,
        ServerCallContext context)
    {
        if (!Guid.TryParse(request.OrderId, out var orderId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"OrderId 格式无效：{request.OrderId}"));
        }

        var dto = await _queryService.GetPaymentInfoByOrderIdAsync(orderId, context.CancellationToken)
            .ConfigureAwait(false);

        if (dto is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"Payment for order {request.OrderId} not found"));
        }

        return MapToProto(dto);
    }

    private static PaymentInfo MapToProto(PaymentInfoResultDto dto)
    {
        // 注：PaymentInfoResultDto 仅含 PaymentId/Channel(int)/OrderId/Status(int)
        // proto PaymentInfo 含 amount_cents/status(string)/paid_at/channel(string)/transaction_id/refunded_amount_cents
        // 当前 DTO 未提供 amount/paid_at/transaction_id/refunded_amount，留默认值
        // Channel/Status: int → string（POC 简化，生产化需统一映射）
        return new PaymentInfo
        {
            PaymentId = dto.PaymentId.ToString(),
            OrderId = dto.OrderId.ToString(),
            AmountCents = 0L,  // DTO 未提供
            Status = dto.Status.ToString(),
            PaidAt = string.Empty,  // DTO 未提供
            Channel = MapChannelToString(dto.Channel)
        };
    }

    private static string MapChannelToString(int channel) => channel switch
    {
        0 => "WeChatPay",
        1 => "Alipay",
        _ => channel.ToString(CultureInfo.InvariantCulture)
    };
}
