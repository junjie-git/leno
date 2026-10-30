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
/// 购物车价格 gRPC 防腐层客户端（gRPC 单轨唯一实时实现）。
/// 实现 <see cref="ICartPriceService"/>。
/// 调用 Product BC <c>ProductInternalService.BatchGetSkuInfo</c> RPC 批量查询 SKU 价格与可售状态。
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item><see cref="CartPriceDispatcherAdapter"/> 经
///         <see cref="AntiCorruptionDispatcher{ICartPriceService}"/> 以本类为唯一实现执行实时查询，
///         熔断 fail-fast 由 Dispatcher 统一处理。</item>
///   <item>该实时路径再被 <see cref="SnapshotCartPriceService"/> 装饰为容器中对外暴露的
///         <see cref="ICartPriceService"/>：快照缺失或过期时回退到本类的实时查询。</item>
/// </list>
/// 双轨下线 D4（2026-09-23）：原快照模式开关已删除，快照优先无条件生效；
/// HTTP 轨道已随之删除，本类不再承担"旧路径兼容"职责。
/// </remarks>
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

            // C3（2026-09-24）：int64 兼容字段已从契约删除，sku_ids_str 为唯一标识形态
            var request = new BatchGetSkuInfoRequest();
            request.SkuIdsStr.AddRange(ids.Select(id => GuidProtoConverter.ToString(id)));

            var metadata = BuildMetadata();
            var response = await _client.BatchGetSkuInfoAsync(request, metadata, cancellationToken: token);

            // 响应映射：以 SkuIdStr 建立 Guid 映射（int64 字段已删除，无回退路径）
            var skuMapByStr = ids.ToDictionary(id => GuidProtoConverter.ToString(id), id => id);
            var result = new List<SkuPriceSnapshotDomain>(response.Skus.Count);
            foreach (var proto in response.Skus)
            {
                if (!skuMapByStr.TryGetValue(proto.SkuIdStr, out var guid))
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
