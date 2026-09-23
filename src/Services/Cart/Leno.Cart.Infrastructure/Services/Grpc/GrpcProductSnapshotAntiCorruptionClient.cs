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
        // C3（2026-09-24）：int64 兼容字段已从契约删除，sku_id_str 为唯一标识形态
        var request = new GetSkuInfoRequest
        {
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
            // C3（2026-09-24）：int64 兼容字段已从契约删除，sku_ids_str 为唯一标识形态
            var request = new BatchGetSkuInfoRequest();
            request.SkuIdsStr.AddRange(ids.Select(id => GuidProtoConverter.ToString(id)));

            var metadata = BuildMetadata();
            var response = await _client.BatchGetSkuInfoAsync(request, metadata, cancellationToken: token);

            // 响应映射：以 SkuIdStr 建立 Guid 映射（int64 字段已删除，无回退路径）
            var skuMapByStr = ids.ToDictionary(id => GuidProtoConverter.ToString(id), id => id);
            var result = new List<SkuSnapshotDto>(response.Skus.Count);
            foreach (var proto in response.Skus)
            {
                if (!skuMapByStr.TryGetValue(proto.SkuIdStr, out var guid))
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
