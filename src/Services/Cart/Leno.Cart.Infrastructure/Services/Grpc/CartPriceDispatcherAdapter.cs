using Leno.Cart.Domain.Services;
using Leno.Infrastructure.AntiCorruption;

namespace Leno.Cart.Infrastructure.Services.Grpc;

/// <summary>
/// 购物车价格防腐层双轨调度适配器（M4 双轨方案）。
/// 实现 <see cref="ICartPriceService"/>，内部委托 <see cref="AntiCorruptionDispatcher{ICartPriceService}"/>
/// 在 HttpClient（<see cref="CartPriceService"/>）与 gRPC（<see cref="GrpcCartPriceService"/>）实现间
/// 按 <c>UseGrpc</c> 开关与熔断状态选择。
/// 注：<see cref="AntiCorruptionDispatcher{TService}"/> 本身不实现 <c>TService</c>，
/// 故需本适配器作为 DI 容器中 <see cref="ICartPriceService"/> 的具体实现。
/// </summary>
public sealed class CartPriceDispatcherAdapter : ICartPriceService
{
    private readonly AntiCorruptionDispatcher<ICartPriceService> _dispatcher;

    public CartPriceDispatcherAdapter(AntiCorruptionDispatcher<ICartPriceService> dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<SkuPriceSnapshot>> GetSkuPricesAsync(IEnumerable<Guid> skuIds, CancellationToken ct = default)
        => _dispatcher.ExecuteAsync(s => s.GetSkuPricesAsync(skuIds, ct), ct);
}
