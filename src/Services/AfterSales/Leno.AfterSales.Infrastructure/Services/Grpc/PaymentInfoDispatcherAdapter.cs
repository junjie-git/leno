using Leno.AfterSales.Application.Services;
using Leno.Infrastructure.AntiCorruption;

namespace Leno.AfterSales.Infrastructure.Services.Grpc;

/// <summary>
/// 支付信息查询熔断调度适配器（售后 BC 独立维护，双轨下线单轨版）。
/// 实现 <see cref="IPaymentInfoQueryService"/>，内部委托 <see cref="AntiCorruptionDispatcher{TService}"/>
/// 对 gRPC 实现（<see cref="GrpcPaymentInfoQueryService"/>）做熔断保护调用（Open 时 fail-fast，无 HTTP 降级路径）。
/// 注：<see cref="AntiCorruptionDispatcher{TService}"/> 本身不实现 <c>TService</c>，
/// 故需本适配器作为 DI 容器中 <see cref="IPaymentInfoQueryService"/> 的具体实现。
/// </summary>
public sealed class PaymentInfoDispatcherAdapter : IPaymentInfoQueryService
{
    private readonly AntiCorruptionDispatcher<IPaymentInfoQueryService> _dispatcher;

    public PaymentInfoDispatcherAdapter(AntiCorruptionDispatcher<IPaymentInfoQueryService> dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    /// <inheritdoc />
    public Task<PaymentInfoResult?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default)
        => _dispatcher.ExecuteAsync(s => s.GetByOrderIdAsync(orderId, ct), ct);
}
