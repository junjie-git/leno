using Grpc.Core;
using Leno.Infrastructure.AntiCorruption;
using Leno.Order.Application.Services;
using Leno.SharedContracts.Grpc.Promotion.V1;
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
        ILogger<GrpcPromotionAntiCorruptionClient> logger)
        : base()
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _ = logger; // 保留参数供未来扩展，当前基类不使用 logger
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
                // 注：proto 中 sku_id 为 int64，DTO 中为 Guid。
                // POC 阶段使用 GetHashCode() 简化映射，生产实施前需评估改为 string 承载（见 spec §4.1 决策）。
                SkuId = (long)i.SkuId.GetHashCode(),
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
