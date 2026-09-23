using Grpc.Core;
using Leno.Infrastructure.AntiCorruption;
using Leno.Order.Application.Services;
using Leno.SharedContracts.Grpc.Promotion.V1;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.Order.Infrastructure.Services.Grpc;

/// <summary>
/// 促销域 gRPC 防腐层客户端（M4 双轨方案）。
/// 实现 <see cref="IPromotionAntiCorruptionService"/>，与 HttpClient 实现双轨。
/// 由 AntiCorruptionDispatcher 在运行时选择使用本类或 HttpClient 实现。
/// </summary>
public sealed class GrpcPromotionAntiCorruptionClient
    : GrpcAntiCorruptionClientBase, IPromotionAntiCorruptionService
{
    private const string TargetBc = "Promotion";
    private const string InternalKeyHeader = "x-internal-key";

    private readonly PromotionInternalService.PromotionInternalServiceClient _client;
    private readonly IOptionsMonitor<AntiCorruptionOptions> _options;

    protected override string ServiceName => "promotion";

    public GrpcPromotionAntiCorruptionClient(
        PromotionInternalService.PromotionInternalServiceClient client,
        IOptionsMonitor<AntiCorruptionOptions> options,
        ILogger<GrpcPromotionAntiCorruptionClient> logger,
        IServiceProvider? serviceProvider = null)
        : base(serviceProvider, logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public Task<decimal> CalculateDiscountAsync(Guid userId, List<(Guid SkuId, decimal Subtotal)> items, CancellationToken ct = default)
        => ExecuteAsync("calculate_discount", async token =>
        {
            var request = new CalculateDiscountRequest
            {
                UserId = userId.ToString()
            };
            request.Items.AddRange(items.Select(i => new OrderItem
            {
                // C3（2026-09-24）：int64 兼容字段已从契约删除，sku_id_str 为唯一标识形态
                SkuIdStr = GuidProtoConverter.ToString(i.SkuId),
                SubtotalCents = (long)(i.Subtotal * 100)
            }));
            var metadata = BuildMetadata();
            var response = await _client.CalculateDiscountAsync(request, metadata, cancellationToken: token)
                .ConfigureAwait(false);
            return response.DiscountCents / 100m;
        }, ct);

    /// <inheritdoc />
    public async Task LockCouponAsync(Guid userId, Guid couponId, Guid orderId, CancellationToken ct = default)
    {
        await ExecuteAsync("lock_coupon", async token =>
        {
            var request = new LockCouponRequest
            {
                UserId = userId.ToString(),
                CouponId = couponId.ToString(),
                OrderId = orderId.ToString()
            };
            var metadata = BuildMetadata();
            await _client.LockCouponAsync(request, metadata, cancellationToken: token)
                .ConfigureAwait(false);
            return true; // ExecuteAsync<TResult> 需要 TResult，用 bool 占位
        }, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ReleaseCouponsAsync(Guid orderId, CancellationToken ct = default)
    {
        await ExecuteAsync("release_coupons", async token =>
        {
            var request = new ReleaseCouponsRequest { OrderId = orderId.ToString() };
            var metadata = BuildMetadata();
            await _client.ReleaseCouponsAsync(request, metadata, cancellationToken: token)
                .ConfigureAwait(false);
            return true;
        }, ct).ConfigureAwait(false);
    }

    private Metadata BuildMetadata()
    {
        var metadata = new Metadata();
        var currentOptions = _options.CurrentValue;
        if (currentOptions.TargetInternalApiKeys.TryGetValue(TargetBc, out var key) && !string.IsNullOrEmpty(key))
        {
            metadata.Add(InternalKeyHeader, key);
        }
        return metadata;
    }
}
