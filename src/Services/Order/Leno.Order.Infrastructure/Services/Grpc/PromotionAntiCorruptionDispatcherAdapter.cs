using Leno.Infrastructure.AntiCorruption;
using Leno.Order.Application.Services;

namespace Leno.Order.Infrastructure.Services.Grpc;

/// <summary>
/// 促销防腐层双轨调度适配器（M4 双轨方案）。
/// 实现 <see cref="IPromotionAntiCorruptionService"/>，内部委托 <see cref="AntiCorruptionDispatcher{TService}"/>
/// 在 HttpClient 与 gRPC 实现间按 <c>UseGrpc</c> 开关与熔断状态选择。
/// 注：<see cref="AntiCorruptionDispatcher{TService}"/> 本身不实现 <c>TService</c>，
/// 故需本适配器作为 DI 容器中 <see cref="IPromotionAntiCorruptionService"/> 的具体实现。
/// </summary>
public sealed class PromotionAntiCorruptionDispatcherAdapter : IPromotionAntiCorruptionService
{
    private readonly AntiCorruptionDispatcher<IPromotionAntiCorruptionService> _dispatcher;

    public PromotionAntiCorruptionDispatcherAdapter(
        AntiCorruptionDispatcher<IPromotionAntiCorruptionService> dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    /// <inheritdoc />
    public Task<decimal> CalculateDiscountAsync(Guid userId, List<(Guid SkuId, decimal Subtotal)> items, CancellationToken ct = default)
        => _dispatcher.ExecuteAsync(s => s.CalculateDiscountAsync(userId, items, ct), ct);

    /// <inheritdoc />
    public async Task ReleaseCouponsAsync(Guid orderId, CancellationToken ct = default)
    {
        await _dispatcher.ExecuteAsync(async s =>
        {
            await s.ReleaseCouponsAsync(orderId, ct).ConfigureAwait(false);
            return 0;
        }, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task LockCouponAsync(Guid userId, Guid couponId, Guid orderId, CancellationToken ct = default)
    {
        await _dispatcher.ExecuteAsync(async s =>
        {
            await s.LockCouponAsync(userId, couponId, orderId, ct).ConfigureAwait(false);
            return 0;
        }, ct).ConfigureAwait(false);
    }
}
