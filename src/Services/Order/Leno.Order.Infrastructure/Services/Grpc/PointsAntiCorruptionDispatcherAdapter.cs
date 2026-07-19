using Leno.Infrastructure.AntiCorruption;
using Leno.Order.Application.Services;

namespace Leno.Order.Infrastructure.Services.Grpc;

/// <summary>
/// 积分防腐层双轨调度适配器（M4 双轨方案）。
/// 实现 <see cref="IPointsAntiCorruptionService"/>，内部委托 <see cref="AntiCorruptionDispatcher{TService}"/>
/// 在 HttpClient 与 gRPC 实现间按 <c>UseGrpc</c> 开关与熔断状态选择。
/// 注：<see cref="AntiCorruptionDispatcher{TService}"/> 本身不实现 <c>TService</c>，
/// 故需本适配器作为 DI 容器中 <see cref="IPointsAntiCorruptionService"/> 的具体实现。
/// </summary>
public sealed class PointsAntiCorruptionDispatcherAdapter : IPointsAntiCorruptionService
{
    private readonly AntiCorruptionDispatcher<IPointsAntiCorruptionService> _dispatcher;

    public PointsAntiCorruptionDispatcherAdapter(
        AntiCorruptionDispatcher<IPointsAntiCorruptionService> dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    /// <inheritdoc />
    public Task<decimal> TryOffsetAsync(Guid userId, int pointsToUse, CancellationToken ct = default)
        => _dispatcher.ExecuteAsync(s => s.TryOffsetAsync(userId, pointsToUse, ct), ct);

    /// <inheritdoc />
    public async Task FreezeAsync(Guid userId, Guid orderId, int pointsToUse, CancellationToken ct = default)
    {
        await _dispatcher.ExecuteAsync(async s =>
        {
            await s.FreezeAsync(userId, orderId, pointsToUse, ct).ConfigureAwait(false);
            return 0;
        }, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ReleaseAsync(Guid orderId, CancellationToken ct = default)
    {
        await _dispatcher.ExecuteAsync(async s =>
        {
            await s.ReleaseAsync(orderId, ct).ConfigureAwait(false);
            return 0;
        }, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ConfirmDeductionAsync(Guid orderId, CancellationToken ct = default)
    {
        await _dispatcher.ExecuteAsync(async s =>
        {
            await s.ConfirmDeductionAsync(orderId, ct).ConfigureAwait(false);
            return 0;
        }, ct).ConfigureAwait(false);
    }
}
