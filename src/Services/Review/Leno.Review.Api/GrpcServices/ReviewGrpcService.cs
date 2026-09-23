using Grpc.Core;
using Leno.Review.Application;
using Leno.SharedContracts.Grpc.Review.V1;
using Microsoft.AspNetCore.Authorization;

namespace Leno.Review.Api.GrpcServices;

/// <summary>
/// 评价域 gRPC 服务端（评价 BC 独立维护，M4 双轨方案）。
/// 复用 <see cref="IReviewInternalQueryService"/> 业务逻辑，与内部 HTTP 路径双轨。
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
        // C3（2026-09-24）：int64 兼容字段已从契约删除，spu_id_str 为唯一标识形态
        if (!Guid.TryParse(request.SpuIdStr, out var spuId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"Invalid or missing spu_id_str: {request.SpuIdStr}"));
        }

        var dto = await _queryService.GetProductRatingAsync(spuId, context.CancellationToken)
            .ConfigureAwait(false);

        if (dto is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"Product rating for spu {request.SpuIdStr} not found"));
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

        // 审计 4.7：实现层 GetOrderReviewsAsync 不再返回 null，无可见评价时返回空 Reviews 列表，
        // gRPC 响应直接返回空 OrderReviews。此处的 null 检查为防御性编程，仅 mock 测试场景触发。
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
        AverageRating = dto.AverageRating,
        TotalCount = dto.TotalCount,
        PositiveCount = dto.PositiveCount,
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
                SpuIdStr = r.SpuId.ToString(),
                Rating = r.Rating,
                Content = r.Content,
                CreatedAt = r.CreatedAt.ToString("O")  // ISO 8601
            });
        }
        return proto;
    }
}
