using Grpc.Core;
using Leno.Infrastructure.AntiCorruption;
using Leno.Order.Application.Services;
using Leno.SharedContracts.Grpc.Product.V1;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SkuInfoDto = Leno.Order.Application.Services.SkuInfo;
using SkuInfoProto = Leno.SharedContracts.Grpc.Product.V1.SkuInfo;

namespace Leno.Order.Infrastructure.Services.Grpc;

/// <summary>
/// 商品域 gRPC 防腐层客户端（M4 双轨方案）。
/// 实现 <see cref="IProductAntiCorruptionService"/>，与 <see cref="ProductAntiCorruptionService"/>（HttpClient）双轨。
/// 由 AntiCorruptionDispatcher 在运行时选择使用本类或 HttpClient 实现。
/// </summary>
public sealed class GrpcProductAntiCorruptionClient
    : GrpcAntiCorruptionClientBase, IProductAntiCorruptionService
{
    private const string TargetBc = "Product";
    private const string InternalKeyHeader = "x-internal-key";

    private readonly ProductInternalService.ProductInternalServiceClient _client;
    private readonly IOptionsMonitor<AntiCorruptionOptions> _options;

    protected override string ServiceName => "product";

    public GrpcProductAntiCorruptionClient(
        ProductInternalService.ProductInternalServiceClient client,
        IOptionsMonitor<AntiCorruptionOptions> options,
        ILogger<GrpcProductAntiCorruptionClient> logger)
        : base()
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _ = logger; // 保留参数供未来扩展，当前基类不使用 logger
    }

    /// <inheritdoc />
    public Task<SkuInfoDto?> GetSkuInfoAsync(Guid skuId, CancellationToken ct = default)
        => ExecuteAsync("get_sku_info", async token =>
        {
            // 请求构造同时填充 int64（向后兼容）+ string（M4 Guid→string 迁移）
            var request = new GetSkuInfoRequest
            {
                SkuId = (long)skuId.GetHashCode(),
                SkuIdStr = skuId.ToString()
            };
            var metadata = BuildMetadata();
            var response = await _client.GetSkuInfoAsync(request, metadata, cancellationToken: token)
                .ConfigureAwait(false);
            return MapToDto(response);
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

    private static SkuInfoDto? MapToDto(SkuInfoProto proto) => new()
    {
        // M4 Guid→string 迁移：优先读 string 字段，为空回退到 Guid.Empty
        // （POC 阶段 int64→Guid 不可逆，回退时无法还原原 Guid）
        SkuId = !string.IsNullOrEmpty(proto.SkuIdStr) ? Guid.Parse(proto.SkuIdStr) : Guid.Empty,
        SpuId = !string.IsNullOrEmpty(proto.SpuIdStr) ? Guid.Parse(proto.SpuIdStr) : Guid.Empty,
        SellerId = !string.IsNullOrEmpty(proto.SellerIdStr) ? Guid.Parse(proto.SellerIdStr) : Guid.Empty,
        ProductName = proto.Title,
        SkuName = proto.Title,
        MainImage = string.IsNullOrEmpty(proto.MainImage) ? null : proto.MainImage,
        UnitPrice = proto.PriceCents / 100m,
        AvailableQty = proto.Stock,
        IsOnSale = proto.Salable
    };
}
