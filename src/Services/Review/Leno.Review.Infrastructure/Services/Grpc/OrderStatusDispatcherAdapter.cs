using Leno.Infrastructure.AntiCorruption;
using Leno.Review.Domain.Services;

namespace Leno.Review.Infrastructure.Services.Grpc;

/// <summary>
/// 订单状态查询双轨调度适配器（评价 BC 独立维护，M4 双轨方案）。
/// 实现 <see cref="IOrderStatusProvider"/>，内部委托 <see cref="AntiCorruptionDispatcher{TService}"/>
/// 在 HttpClient（<see cref="HttpOrderStatusProvider"/>）与 gRPC（<see cref="GrpcOrderStatusProvider"/>）实现间按 <c>UseGrpc</c> 开关与熔断状态选择。
/// 注：<see cref="AntiCorruptionDispatcher{TService}"/> 本身不实现 <c>TService</c>，
/// 故需本适配器作为 DI 容器中 <see cref="IOrderStatusProvider"/> 的具体实现。
/// </summary>
public sealed class OrderStatusDispatcherAdapter : IOrderStatusProvider
{
    private readonly AntiCorruptionDispatcher<IOrderStatusProvider> _dispatcher;

    public OrderStatusDispatcherAdapter(AntiCorruptionDispatcher<IOrderStatusProvider> dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    /// <inheritdoc />
    public Task<OrderStatusInfo?> GetOrderStatusAsync(Guid orderId, CancellationToken ct = default)
        => _dispatcher.ExecuteAsync(s => s.GetOrderStatusAsync(orderId, ct), ct);
}
