using Grpc.Core;
using Leno.Cart.Application.Abstractions;
using Leno.Cart.Application.DTOs;
using Leno.Infrastructure.AntiCorruption;
using Leno.SharedContracts.Grpc.Product.V1;
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
        ILogger<GrpcProductSnapshotAntiCorruptionClient> logger)
        : base()
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _ = logger;
    }

    /// <inheritdoc />
    public Task<SkuSnapshotDto> GetSkuSnapshotAsync(Guid skuId, CancellationToken ct = default)
        => ExecuteAsync("get_sku_snapshot", async token =>
    {
        // M4 Guid→string 迁移：请求同时填充 int64（向后兼容）+ string
        var request = new GetSkuInfoRequest
        {
            SkuId = (long)skuId.GetHashCode(),
            SkuIdStr = skuId.ToString()
        };

        var metadata = BuildMetadata();
        var proto = await _client.GetSkuInfoAsync(request, metadata, cancellationToken: token)
            .ConfigureAwait(false);

        return MapToDto(proto, skuId);
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
