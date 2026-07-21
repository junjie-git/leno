using Grpc.Core;
using Leno.Infrastructure.AntiCorruption;
using Leno.SellerShop.Application.Services;
using Leno.SharedContracts.Grpc.Review.V1;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.SellerShop.Infrastructure.Services.Grpc;

/// <summary>
/// 评论域 gRPC 防腐层客户端（卖家店铺域视角）。
/// 实现 <see cref="IReviewAntiCorruptionService"/>，用于卖家工作台读模型构建时反查评论域聚合评分统计。
/// 通过 <see cref="GrpcAntiCorruptionClientBase.ExecuteAsync{T}"/> 统一异常处理与埋点；
/// 防腐层失败时由本类捕获 <see cref="AntiCorruptionException"/> 返回 null（fail-closed），
/// 避免 ReviewAfterSales 域故障阻塞工作台读模型构建。
/// </summary>
public sealed class GrpcReviewAntiCorruptionClient
    : GrpcAntiCorruptionClientBase, IReviewAntiCorruptionService
{
    private const string TargetBc = "Review";
    private const string InternalKeyHeader = "x-internal-key";

    private readonly ReviewInternalService.ReviewInternalServiceClient _client;
    private readonly IOptionsMonitor<AntiCorruptionOptions> _options;
    private readonly ILogger<GrpcReviewAntiCorruptionClient> _logger;

    protected override string ServiceName => "review";

    public GrpcReviewAntiCorruptionClient(
        ReviewInternalService.ReviewInternalServiceClient client,
        IOptionsMonitor<AntiCorruptionOptions> options,
        ILogger<GrpcReviewAntiCorruptionClient> logger,
        IServiceProvider? serviceProvider = null)
        : base(serviceProvider, logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<ReviewStatisticsDto?> GetReviewStatisticsAsync(Guid shopId, CancellationToken ct = default)
    {
        if (shopId == Guid.Empty)
        {
            return null;
        }

        try
        {
            return await ExecuteAsync("get_shop_review_statistics", async token =>
            {
                var request = new GetShopReviewStatisticsRequest
                {
                    ShopId = shopId.ToString()
                };
                var metadata = BuildMetadata();
                var response = await _client.GetShopReviewStatisticsAsync(request, metadata, cancellationToken: token)
                    .ConfigureAwait(false);
                return new ReviewStatisticsDto
                {
                    TotalReviews = response.TotalReviews,
                    // proto double → decimal 保留两位小数，避免精度损失
                    AverageRating = Math.Round((decimal)response.AverageRating, 2, MidpointRounding.AwayFromZero),
                    FiveStarReviews = response.FiveStarReviews,
                    OneStarReviews = response.OneStarReviews
                };
            }, ct).ConfigureAwait(false);
        }
        catch (AntiCorruptionException ex)
        {
            // fail-closed：跨域调用失败时返回 null，由 ShopDashboardReadModelBuilder 按零值兜底
            _logger.LogWarning(ex, "评论域 GetShopReviewStatistics 调用失败，fail-closed 返回 null ShopId={ShopId}", shopId);
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
