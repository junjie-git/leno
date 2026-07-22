using Grpc.Core;
using Leno.Infrastructure.AntiCorruption;
using Leno.SellerShop.Application.Services;
using Leno.SharedContracts.Grpc.Product.V1;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.SellerShop.Infrastructure.Services.Grpc;

/// <summary>
/// 商品域 gRPC 防腐层客户端（卖家店铺域视角）。
/// 实现 <see cref="IProductAntiCorruptionService"/>，用于卖家资源归属校验时反查 SPU 归属卖家。
/// 通过 <see cref="GrpcAntiCorruptionClientBase.ExecuteAsync{T}"/> 统一异常处理与埋点；
/// 防腐层失败时由本类捕获 <see cref="AntiCorruptionException"/> 返回 null（fail-closed），
/// 避免 Product 域故障阻塞卖家归属校验流程。
/// </summary>
public sealed class GrpcProductAntiCorruptionClient
    : GrpcAntiCorruptionClientBase, IProductAntiCorruptionService
{
    private const string TargetBc = "Product";
    private const string InternalKeyHeader = "x-internal-key";

    private readonly ProductInternalService.ProductInternalServiceClient _client;
    private readonly IOptionsMonitor<AntiCorruptionOptions> _options;
    private readonly ILogger<GrpcProductAntiCorruptionClient> _logger;

    protected override string ServiceName => "product";

    public GrpcProductAntiCorruptionClient(
        ProductInternalService.ProductInternalServiceClient client,
        IOptionsMonitor<AntiCorruptionOptions> options,
        ILogger<GrpcProductAntiCorruptionClient> logger,
        IServiceProvider? serviceProvider = null)
        : base(serviceProvider, logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<Guid?> GetSpuSellerIdAsync(Guid spuId, CancellationToken ct = default)
    {
        try
        {
            return await ExecuteAsync("get_spu_seller", async token =>
            {
                var request = new GetProductDetailRequest
                {
                    SpuIdStr = spuId.ToString()
                };
                var metadata = BuildMetadata();
                var response = await _client.GetProductDetailAsync(request, metadata, cancellationToken: token)
                    .ConfigureAwait(false);
                return Guid.TryParse(response.SellerIdStr, out var sellerId) ? sellerId : (Guid?)null;
            }, ct).ConfigureAwait(false);
        }
        catch (AntiCorruptionException ex)
        {
            // fail-closed：跨域调用失败时返回 null，由 SellerInternalQueryService 判 false
            // 基类 ExecuteAsync 已记录 "grpc" path 的失败，此处补充记录 "fail-closed" path 的降级触发，
            // 供告警规则按 path=fail-closed 统计降级频率（ACL 失败率 > 5% 触发告警）
            AntiCorruptionMetrics.RecordFailure(ServiceName, "get_spu_seller", "fail-closed");
            _logger.LogWarning(ex, "商品域 GetSpuSellerId 调用失败，fail-closed 返回 null SpuId={SpuId}", spuId);
            return null;
        }
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
