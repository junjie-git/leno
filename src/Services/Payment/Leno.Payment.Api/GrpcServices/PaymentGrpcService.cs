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
        // DTO 已提供 Amount/Currency/PaidAt/TradeNo/RefundedAmount，
        // 转换为 proto 语义：金额转分（避免浮点精度损失），时间转 ISO 8601 字符串
        // Channel/Status: int → string（POC 简化，生产化需统一映射）
        return new PaymentInfo
        {
            PaymentId = dto.PaymentId.ToString(),
            OrderId = dto.OrderId.ToString(),
            AmountCents = (long)Math.Round(dto.Amount * 100m),
            Status = dto.Status.ToString(),
            PaidAt = dto.PaidAt.HasValue
                ? dto.PaidAt.Value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture)
                : string.Empty,
            Channel = MapChannelToString(dto.Channel),
            TransactionId = dto.TradeNo ?? string.Empty,
            RefundedAmountCents = (long)Math.Round(dto.RefundedAmount * 100m)
        };
    }

    private static string MapChannelToString(int channel) => channel switch
    {
        0 => "WeChatPay",
        1 => "Alipay",
        _ => channel.ToString(CultureInfo.InvariantCulture)
    };
}
