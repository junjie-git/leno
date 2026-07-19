using Leno.Infrastructure.AntiCorruption;
using Leno.ReviewAfterSales.Application.Services;

namespace Leno.ReviewAfterSales.Infrastructure.Services.Grpc;

/// <summary>
/// 支付信息查询双轨调度适配器（M4 双轨方案）。
/// 实现 <see cref="IPaymentInfoQueryService"/>，内部委托 <see cref="AntiCorruptionDispatcher{TService}"/>
/// 在 HttpClient（<see cref="PaymentInfoQueryService"/>）与 gRPC（<see cref="GrpcPaymentInfoQueryService"/>）实现间按 <c>UseGrpc</c> 开关与熔断状态选择。
/// 注：<see cref="AntiCorruptionDispatcher{TService}"/> 本身不实现 <c>TService</c>，
/// 故需本适配器作为 DI 容器中 <see cref="IPaymentInfoQueryService"/> 的具体实现。
/// </summary>
public sealed class PaymentInfoQueryDispatcherAdapter : IPaymentInfoQueryService
{
    private readonly AntiCorruptionDispatcher<IPaymentInfoQueryService> _dispatcher;

    public PaymentInfoQueryDispatcherAdapter(AntiCorruptionDispatcher<IPaymentInfoQueryService> dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    /// <inheritdoc />
    public Task<PaymentInfoResult?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default)
        => _dispatcher.ExecuteAsync(s => s.GetByOrderIdAsync(orderId, ct), ct);
}
