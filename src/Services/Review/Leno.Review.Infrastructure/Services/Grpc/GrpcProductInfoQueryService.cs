using Grpc.Core;
using Leno.Infrastructure.AntiCorruption;
using Leno.Review.Application.Services;
using Leno.SharedContracts.Grpc.Product.V1;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.Review.Infrastructure.Services.Grpc;

/// <summary>
/// 商品信息查询 gRPC 防腐层客户端（gRPC 单轨唯一实现，评价 BC 独立维护）。
/// 实现 <see cref="IProductInfoQueryService"/>，调用商品域 <c>ProductInternalService.GetProductDetail</c> RPC
/// 反查 SPU → 商品名称映射，供卖家侧评价列表按商品名称过滤场景使用。
/// </summary>
/// <remarks>
/// ACL 契约（与 <see cref="IProductInfoQueryService"/> 文档一致）：批量查询逐 SPU 调用，
/// 任一 SPU 查询失败（商品不存在、回包字段校验失败或传输故障）时该 SPU 不出现在结果字典中（不抛异常），
/// 调用方按"无匹配 SpuId"跳过该 SPU 的商品名称过滤。
/// </remarks>
public sealed class GrpcProductInfoQueryService : GrpcAntiCorruptionClientBase, IProductInfoQueryService
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
        var result = new Dictionary<Guid, string>(spuIds.Count);
        foreach (var spuId in spuIds)
        {
            try
            {
                var title = await ExecuteAsync("get_product_name", async token =>
                {
                    var request = new GetProductDetailRequest { SpuIdStr = GuidProtoConverter.ToString(spuId) };
                    var detail = await _client.GetProductDetailAsync(request, metadata, cancellationToken: token)
                        .ConfigureAwait(false);

                    // 防腐校验：回包 spu_id_str 必须可解析且与请求一致，否则视为该 SPU 查询失败。
                    if (!GuidProtoConverter.TryParse(detail.SpuIdStr, out var remoteSpuId) || remoteSpuId != spuId)
                    {
                        throw new AntiCorruptionException(
                            $"商品域返回无效 SpuId：{detail.SpuIdStr}", "PRODUCT_REMOTE_FAILED");
                    }

                    // 防腐校验：title 为空视为查询失败，避免空名称进入过滤映射。
                    if (string.IsNullOrWhiteSpace(detail.Title))
                    {
                        throw new AntiCorruptionException(
                            $"商品域返回空商品名称：SpuId={spuId}", "PRODUCT_REMOTE_FAILED");
                    }

                    return detail.Title;
                }, ct).ConfigureAwait(false);

                result[spuId] = title;
            }
            catch (AntiCorruptionException ex)
            {
                // ACL 契约：单 SPU 查询失败不中断批量查询，仅将该 SPU 从映射中剔除并告警（见类型 remarks）。
                _logger.LogWarning(ex, "商品域 SPU {SpuId} 名称查询失败，已从名称映射中剔除", spuId);
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
