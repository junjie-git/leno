using Leno.Review.Application.Services;
using Microsoft.Extensions.Logging;

namespace Leno.Review.Infrastructure.Services;

/// <summary>
/// 商品信息查询防腐层降级实现（fail-open）。
/// 当 <c>AntiCorruption:UseGrpc=false</c> 或 <c>AntiCorruption:GrpcEndpoints:Product</c> 配置缺失时，
/// 由 <see cref="Dependencies.ServiceCollectionExtensions"/> 注册为本接口的临时实现，
/// 避免商品域 gRPC 端点缺失阻塞评价域启动。
/// </summary>
/// <remarks>
/// 降级语义：返回空字典，调用方 <see cref="Application.Services.ReviewAppService.GetBySellerAsync"/> 收到空映射后
/// 按"无匹配 SpuId"处理，即按商品名称过滤时返回空列表；不传 productName 时本接口不被调用，卖家评价列表正常工作。
/// 该降级仅在 gRPC 端点缺失时启用，运维补齐配置并重启后自动恢复为 <see cref="Grpc.GrpcProductInfoQueryService"/>。
/// </remarks>
public sealed class NullProductInfoQueryService : IProductInfoQueryService
{
    private readonly ILogger<NullProductInfoQueryService> _logger;

    public NullProductInfoQueryService(ILogger<NullProductInfoQueryService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<Guid, string>> GetProductNamesBySpuIdsAsync(
        IReadOnlyCollection<Guid> spuIds,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(spuIds);

        // fail-open：返回空字典，调用方按"无匹配 SpuId"处理返回空结果，避免阻塞评价域启动。
        // 仅在 gRPC 端点缺失降级场景触发，正常运行路径不会进入本实现。
        _logger.LogWarning(
            "商品域防腐层处于降级模式（NullProductInfoQueryService），返回空 SPU 名称映射。请补齐 AntiCorruption:GrpcEndpoints:Product 配置以恢复完整功能。");
        return Task.FromResult<IReadOnlyDictionary<Guid, string>>(new Dictionary<Guid, string>());
    }
}
