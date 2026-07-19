using Leno.Cart.Application.Abstractions;
using Leno.Cart.Application.DTOs;
using Leno.Infrastructure.AntiCorruption;

namespace Leno.Cart.Infrastructure.Services.Grpc;

/// <summary>
/// 商品域快照防腐层双轨适配器（M4 双轨方案）。
/// 实现 <see cref="IProductSnapshotAntiCorruption"/>，委托 <see cref="AntiCorruptionDispatcher{IProductSnapshotAntiCorruption}"/> 选择 gRPC 或 HttpClient 实现。
/// </summary>
public sealed class ProductSnapshotDispatcherAdapter : IProductSnapshotAntiCorruption
{
    private readonly AntiCorruptionDispatcher<IProductSnapshotAntiCorruption> _dispatcher;

    public ProductSnapshotDispatcherAdapter(
        AntiCorruptionDispatcher<IProductSnapshotAntiCorruption> dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    /// <inheritdoc />
    public Task<SkuSnapshotDto> GetSkuSnapshotAsync(Guid skuId, CancellationToken ct = default)
        => _dispatcher.ExecuteAsync(s => s.GetSkuSnapshotAsync(skuId, ct), ct);
}
