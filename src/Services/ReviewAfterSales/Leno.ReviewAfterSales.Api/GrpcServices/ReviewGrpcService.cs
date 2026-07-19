using Grpc.Core;
using Leno.ReviewAfterSales.Application;
using Leno.SharedContracts.Grpc.Review.V1;
using Microsoft.AspNetCore.Authorization;

namespace Leno.ReviewAfterSales.Api.GrpcServices;

/// <summary>
/// 评价与售后域 gRPC 服务端（M4 双轨方案）。
/// 复用 <see cref="IReviewInternalQueryService"/> 业务逻辑，与 InternalReviewsController HTTP 路径双轨。
/// 鉴权由 GrpcInternalKeyInterceptor 拦截器统一处理（metadata x-internal-key）。
/// </summary>
[Authorize]
public sealed class ReviewGrpcService : ReviewInternalService.ReviewInternalServiceBase
{
    private readonly IReviewInternalQueryService _queryService;
    private readonly ILogger<ReviewGrpcService> _logger;

    public ReviewGrpcService(
        IReviewInternalQueryService queryService,
        ILogger<ReviewGrpcService> logger)
    {
        _queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override async Task<ProductRating> GetProductRating(
        GetProductRatingRequest request, ServerCallContext context)
    {
        // 优先读 string 字段（Guid.ToString()），回退到 int64（向后兼容旧客户端）
        Guid spuId;
        if (!string.IsNullOrEmpty(request.SpuIdStr))
        {
            if (!Guid.TryParse(request.SpuIdStr, out spuId))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, $"Invalid spu_id_str: {request.SpuIdStr}"));
            }
        }
        else
        {
            // 旧客户端回退：将 int64 嵌入 Guid 前 4 字节，其余补零
            spuId = new Guid((int)request.SpuId, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        }

        var dto = await _queryService.GetProductRatingAsync(spuId, context.CancellationToken)
            .ConfigureAwait(false);

        if (dto is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"Product rating for spu {request.SpuId} not found"));
        }

        return MapToProto(dto);
    }

    public override async Task<OrderReviews> GetOrderReviews(
        GetOrderReviewsRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.OrderId, out var orderId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"Invalid order_id: {request.OrderId}"));
        }

        var dto = await _queryService.GetOrderReviewsAsync(orderId, context.CancellationToken)
            .ConfigureAwait(false);

        if (dto is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"Reviews for order {request.OrderId} not found"));
        }

        return MapToProto(dto);
    }

    private static ProductRating MapToProto(ProductRatingDto dto) => new()
    {
        // 既有 int64 字段（向后兼容，标记 deprecated）
        SpuId = (long)dto.SpuId.GetHashCode(),
        AverageRating = dto.AverageRating,
        TotalCount = dto.TotalCount,
        PositiveCount = dto.PositiveCount,
        // 新增 string 字段（Guid→string 迁移，新客户端优先读）
        SpuIdStr = dto.SpuId.ToString()
    };

    private static OrderReviews MapToProto(OrderReviewsDto dto)
    {
        var proto = new OrderReviews();
        foreach (var r in dto.Reviews)
        {
            proto.Reviews.Add(new ReviewSummary
            {
                ReviewId = r.ReviewId.ToString(),
                // 双写：既有 int64 字段（GetHashCode，向后兼容）+ 新增 string 字段（Guid.ToString()）
                SpuId = (long)r.SpuId.GetHashCode(),
                SpuIdStr = r.SpuId.ToString(),
                Rating = r.Rating,
                Content = r.Content,
                CreatedAt = r.CreatedAt.ToString("O")  // ISO 8601
            });
        }
        return proto;
    }
}
