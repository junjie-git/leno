using Leno.Infrastructure.AntiCorruption;
using Leno.Order.Application.Services;

namespace Leno.Order.Infrastructure.Services.Grpc;

/// <summary>
/// 商品防腐层调度适配器（gRPC 单轨）。
/// 实现 <see cref="IProductAntiCorruptionService"/>，内部委托 <see cref="AntiCorruptionDispatcher{TService}"/>
/// 调用唯一 gRPC 实现，熔断开启时 fail-fast。
/// 注：<see cref="AntiCorruptionDispatcher{TService}"/> 本身不实现 <c>TService</c>，
/// 故需本适配器作为 DI 容器中 <see cref="IProductAntiCorruptionService"/> 的具体实现。
/// </summary>
public sealed class ProductAntiCorruptionDispatcherAdapter : IProductAntiCorruptionService
{
    private readonly AntiCorruptionDispatcher<IProductAntiCorruptionService> _dispatcher;

    public ProductAntiCorruptionDispatcherAdapter(
        AntiCorruptionDispatcher<IProductAntiCorruptionService> dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    /// <inheritdoc />
    public Task<SkuInfo?> GetSkuInfoAsync(Guid skuId, CancellationToken ct = default)
        => _dispatcher.ExecuteAsync(s => s.GetSkuInfoAsync(skuId, ct), ct);
}
