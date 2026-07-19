using Grpc.Core;
using Leno.Cart.Domain.Services;
using Leno.Infrastructure.AntiCorruption;
using Leno.SharedContracts.Grpc.Product.V1;
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
        ILogger<GrpcCartPriceService> logger)
        : base()
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _ = logger; // 保留参数供未来扩展，当前基类不使用 logger
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

            // 注：proto 中 sku_id 为 int64，POC 阶段使用 GetHashCode 简化
            // 生产化阶段需将 .proto 改为 string sku_id 承载 Guid.ToString()（Task 27）
            var request = new BatchGetSkuInfoRequest();
            request.SkuIds.AddRange(ids.Select(id => (long)id.GetHashCode()));

            var metadata = BuildMetadata();
            var response = await _client.BatchGetSkuInfoAsync(request, metadata, cancellationToken: token)
                .ConfigureAwait(false);

            // 建立 int64 → Guid 映射，POC 简化：用原 Guid 的 GetHashCode 还原
            var skuMap = ids.ToDictionary(id => (long)id.GetHashCode(), id => id);
            var result = new List<SkuPriceSnapshotDomain>(response.Skus.Count);
            foreach (var proto in response.Skus)
            {
                if (!skuMap.TryGetValue(proto.SkuId, out var guid))
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
        // POC 简化：int64→Guid 不可逆，留空；生产化阶段需将 .proto 改为 string（Task 27）
        SellerId = Guid.Empty
    };
}
