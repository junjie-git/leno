using Grpc.Core;
using Leno.Cart.Application.Abstractions;
using Leno.Cart.Application.DTOs;
using Leno.Infrastructure.AntiCorruption;
using Leno.SharedContracts.Grpc.Product.V1;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.Cart.Infrastructure.Services.Grpc;

/// <summary>
/// 商品域快照 gRPC 防腐层客户端（M4 双轨方案）。
/// 实现 <see cref="IProductSnapshotAntiCorruption"/>，与 <see cref="ProductSnapshotAntiCorruptionService"/>（HttpClient）双轨。
/// 调用 Product BC <c>ProductInternalService.GetSkuInfo</c> RPC 查询单 SKU 展示快照。
/// </summary>
public sealed class GrpcProductSnapshotAntiCorruptionClient
    : GrpcAntiCorruptionClientBase, IProductSnapshotAntiCorruption
{
    private const string TargetBc = "Product";
    private const string InternalKeyHeader = "x-internal-key";

    private readonly ProductInternalService.ProductInternalServiceClient _client;
    private readonly IOptionsMonitor<AntiCorruptionOptions> _options;

    protected override string ServiceName => "product";

    public GrpcProductSnapshotAntiCorruptionClient(
        ProductInternalService.ProductInternalServiceClient client,
        IOptionsMonitor<AntiCorruptionOptions> options,
        ILogger<GrpcProductSnapshotAntiCorruptionClient> logger,
        IServiceProvider? serviceProvider = null)
        : base(serviceProvider, logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public Task<SkuSnapshotDto> GetSkuSnapshotAsync(Guid skuId, CancellationToken ct = default)
        => ExecuteAsync("get_sku_snapshot", async token =>
    {
        // M4 Guid→string 迁移：请求同时填充 int64（稳定算法，向后兼容）+ string（GuidProtoConverter）
        var request = new GetSkuInfoRequest
        {
            // 修复审计 #5：使用稳定算法替代 GetHashCode()（32 位碰撞率高），确保相同 Guid 始终映射到相同 int64
            SkuId = BitConverter.ToInt64(skuId.ToByteArray(), 0),
            SkuIdStr = GuidProtoConverter.ToString(skuId)
        };

        var metadata = BuildMetadata();
        var proto = await _client.GetSkuInfoAsync(request, metadata, cancellationToken: token);

        return MapToDto(proto, skuId);
    }, ct);

    /// <inheritdoc />
    public Task<IReadOnlyList<SkuSnapshotDto>> GetSkuSnapshotsAsync(IReadOnlyCollection<Guid> skuIds, CancellationToken ct = default)
        => ExecuteAsync("get_sku_snapshots_batch", async token =>
        {
            ArgumentNullException.ThrowIfNull(skuIds);
            if (skuIds.Count == 0)
            {
                return (IReadOnlyList<SkuSnapshotDto>)Array.Empty<SkuSnapshotDto>();
            }

            var ids = skuIds.ToList();
            // M4 Guid→string 迁移：请求同时填充 int64（稳定算法，向后兼容）+ string（GuidProtoConverter）
            var request = new BatchGetSkuInfoRequest();
            request.SkuIds.AddRange(ids.Select(id => BitConverter.ToInt64(id.ToByteArray(), 0)));
            request.SkuIdsStr.AddRange(ids.Select(id => GuidProtoConverter.ToString(id)));

            var metadata = BuildMetadata();
            var response = await _client.BatchGetSkuInfoAsync(request, metadata, cancellationToken: token);

            // 响应映射：优先用 SkuIdStr 建立 Guid 映射，回退到 int64 稳定算法映射（向后兼容旧服务端）
            var skuMapByStr = ids.ToDictionary(id => GuidProtoConverter.ToString(id), id => id);
            var skuMapByHash = ids.ToDictionary(id => BitConverter.ToInt64(id.ToByteArray(), 0), id => id);
            var result = new List<SkuSnapshotDto>(response.Skus.Count);
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
                result.Add(MapToDto(proto, guid));
            }
            return (IReadOnlyList<SkuSnapshotDto>)result;
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

    private static SkuSnapshotDto MapToDto(SkuInfo proto, Guid skuId) => new()
    {
        SkuId = skuId,
        Title = proto.Title ?? string.Empty,
        MainImageUrl = string.IsNullOrEmpty(proto.MainImage) ? null : proto.MainImage,
        UnitPrice = proto.PriceCents / 100m,
        IsOnSale = proto.Salable
    };
}
