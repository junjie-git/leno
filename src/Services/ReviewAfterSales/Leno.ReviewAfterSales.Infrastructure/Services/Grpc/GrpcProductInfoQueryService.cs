using Grpc.Core;
using Leno.Infrastructure.AntiCorruption;
using Leno.ReviewAfterSales.Application.Services;
using Leno.SharedContracts.Grpc.Product.V1;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.ReviewAfterSales.Infrastructure.Services.Grpc;

/// <summary>
/// 商品信息查询 gRPC 防腐层客户端（卖家侧评价列表按商品名称过滤场景使用）。
/// 实现 <see cref="IProductInfoQueryService"/>，通过商品域 <see cref="ProductInternalService.GetProductDetail"/>
/// 逐个查询 SPU 详情并构建 SpuId → 商品名称 字典。
/// 任一 SPU 查询失败不抛异常，仅跳过该 SPU（fail-open on single item，避免单点失败阻塞整批查询）。
/// </summary>
public sealed class GrpcProductInfoQueryService
    : GrpcAntiCorruptionClientBase, IProductInfoQueryService
{
    private const string TargetBc = "Product";
    private const string InternalKeyHeader = "x-internal-key";

    private readonly ProductInternalService.ProductInternalServiceClient _client;
    private readonly IOptionsMonitor<AntiCorruptionOptions> _options;
    private readonly ILogger<GrpcProductInfoQueryService> _logger;

    protected override string ServiceName => "product";

    public GrpcProductInfoQueryService(
        ProductInternalService.ProductInternalServiceClient client,
        IOptionsMonitor<AntiCorruptionOptions> options,
        ILogger<GrpcProductInfoQueryService> logger,
        IServiceProvider? serviceProvider = null)
        : base(serviceProvider, logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, string>> GetProductNamesBySpuIdsAsync(
        IReadOnlyCollection<Guid> spuIds,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(spuIds);

        if (spuIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var metadata = BuildMetadata();
        var result = new Dictionary<Guid, string>();

        // 逐个查询 SPU 详情：商品域 ProductInternalService 未提供批量按 SPU 查询名称的 RPC，
        // 此处循环调用 GetProductDetail。SPU 数量受限于本店铺已通过评价的去重 SPU 数，
        // 在分页场景下通常可控（<100）；后续商品域补 BatchGetProductDetail 后可优化为单次调用。
        foreach (var spuId in spuIds)
        {
            if (spuId == Guid.Empty)
            {
                continue;
            }

            try
            {
                var title = await ExecuteAsync("get_product_name", async token =>
                {
                    var request = new GetProductDetailRequest { SpuIdStr = spuId.ToString() };
                    var response = await _client.GetProductDetailAsync(request, metadata, cancellationToken: token)
                        .ConfigureAwait(false);
                    return response.Title;
                }, ct).ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(title))
                {
                    result[spuId] = title;
                }
            }
            catch (AntiCorruptionException ex)
            {
                // 单个 SPU 查询失败不阻塞整批：记录警告并跳过该 SPU
                _logger.LogWarning(ex, "商品域 GetProductDetail 调用失败，跳过该 SPU SpuId={SpuId}", spuId);
                AntiCorruptionMetrics.RecordFailure(ServiceName, "get_product_name", "skip-item");
            }
        }

        return result;
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
