using Leno.Infrastructure.AntiCorruption;
using Leno.AfterSales.Domain.Services;

namespace Leno.AfterSales.Infrastructure.Services.Grpc;

/// <summary>
/// 订单状态查询熔断调度适配器（售后 BC 独立维护，双轨下线单轨版）。
/// 实现 <see cref="IOrderStatusProvider"/>，内部委托 <see cref="AntiCorruptionDispatcher{TService}"/>
/// 对 gRPC 实现（<see cref="GrpcOrderStatusProvider"/>）做熔断保护调用（Open 时 fail-fast，无 HTTP 降级路径）。
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
