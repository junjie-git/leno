using Grpc.Core;
using Leno.Cart.Domain.Services;
using Leno.Infrastructure.AntiCorruption;
using Leno.SharedContracts.Grpc.Product.V1;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SkuPriceSnapshotDomain = Leno.Cart.Domain.Services.SkuPriceSnapshot;
using SkuInfoProto = Leno.SharedContracts.Grpc.Product.V1.SkuInfo;

namespace Leno.Cart.Infrastructure.Services.Grpc;

/// <summary>
/// 购物车价格 gRPC 防腐层客户端（M4 双轨方案）。
/// 实现 <see cref="ICartPriceService"/>，与 <see cref="CartPriceService"/>（HttpClient）双轨。
/// 由 <see cref="AntiCorruptionDispatcher{ICartPriceService}"/> 在运行时按 <c>UseGrpc</c> 开关与熔断状态选择使用本类或 HttpClient 实现。
/// 调用 Product BC <c>ProductInternalService.BatchGetSkuInfo</c> RPC 批量查询 SKU 价格与可售状态。
/// </summary>
public sealed class GrpcCartPriceService
    : GrpcAntiCorruptionClientBase, ICartPriceService
{
    private const string TargetBc = "Product";
    private const string InternalKeyHeader = "x-internal-key";

    private readonly ProductInternalService.ProductInternalServiceClient _client;
    private readonly IOptionsMonitor<AntiCorruptionOptions> _options;

    protected override string ServiceName => "product";

    public GrpcCartPriceService(
        ProductInternalService.ProductInternalServiceClient client,
        IOptionsMonitor<AntiCorruptionOptions> options,
        ILogger<GrpcCartPriceService> logger,
        IServiceProvider? serviceProvider = null)
        : base(serviceProvider, logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<SkuPriceSnapshotDomain>> GetSkuPricesAsync(IEnumerable<Guid> skuIds, CancellationToken ct = default)
        => ExecuteAsync("get_sku_prices", async token =>
        {
            ArgumentNullException.ThrowIfNull(skuIds);
            var ids = skuIds.ToList();
            if (ids.Count == 0)
            {
                return (IReadOnlyList<SkuPriceSnapshotDomain>)Array.Empty<SkuPriceSnapshotDomain>();
            }

            // M4 Guid→string 迁移：请求同时填充 int64（向后兼容）+ string
            var request = new BatchGetSkuInfoRequest();
            request.SkuIds.AddRange(ids.Select(id => (long)id.GetHashCode()));
            request.SkuIdsStr.AddRange(ids.Select(id => id.ToString()));

            var metadata = BuildMetadata();
            var response = await _client.BatchGetSkuInfoAsync(request, metadata, cancellationToken: token)
                .ConfigureAwait(false);

            // 响应映射：优先用 SkuIdStr 建立 Guid 映射，回退到 int64 GetHashCode 映射（向后兼容旧服务端）
            var skuMapByStr = ids.ToDictionary(id => id.ToString(), id => id);
            var skuMapByHash = ids.ToDictionary(id => (long)id.GetHashCode(), id => id);
            var result = new List<SkuPriceSnapshotDomain>(response.Skus.Count);
            foreach (var proto in response.Skus)
            {
                Guid guid;
                if (!string.IsNullOrEmpty(proto.SkuIdStr))
                {
                    if (!skuMapByStr.TryGetValue(proto.SkuIdStr, out guid))
                    {
                        continue;
                    }
                }
                else if (!skuMapByHash.TryGetValue(proto.SkuId, out guid))
                {
                    continue;
                }
                result.Add(MapToSnapshot(proto, guid));
            }
            return (IReadOnlyList<SkuPriceSnapshotDomain>)result;
        }, ct);

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

    private static SkuPriceSnapshotDomain MapToSnapshot(SkuInfoProto proto, Guid guid) => new()
    {
        SkuId = guid,
        Price = proto.PriceCents / 100m,
        Currency = string.IsNullOrEmpty(proto.Currency) ? "CNY" : proto.Currency,
        Available = proto.Salable,
        Title = proto.Title ?? string.Empty,
        MainImageUrl = proto.MainImage ?? string.Empty,
        // 修复：优先读 string 字段，回退到 Guid.Empty（POC 阶段 int64→Guid 不可逆）
        SellerId = !string.IsNullOrEmpty(proto.SellerIdStr) ? Guid.Parse(proto.SellerIdStr) : Guid.Empty
    };
}
