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
        // Guid→string 迁移：必须使用 SpuIdStr（Guid 字符串）定位商品。
        // 旧 int64 字段已 deprecated 且 GetHashCode 不可逆，不再支持回退，强制客户端升级。
        Guid spuId;
        if (!string.IsNullOrEmpty(request.SpuIdStr))
        {
            if (!Guid.TryParse(request.SpuIdStr, out spuId))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, $"Invalid spu_id_str: {request.SpuIdStr}"));
            }
        }
        else if (request.SpuId != 0)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument,
                "SpuId int64 field is deprecated and non-reversible, please use SpuIdStr (Guid string) instead"));
        }
        else
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument,
                "Either SpuIdStr must be provided (SpuId int64 is deprecated)"));
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
        // 既有 int64 字段已 deprecated：强制返回 0，Guid.GetHashCode 不可逆且跨进程不一致，
        // 新客户端必须读 SpuIdStr（Guid 字符串）。
        SpuId = 0,
        AverageRating = dto.AverageRating,
        TotalCount = dto.TotalCount,
        PositiveCount = dto.PositiveCount,
        // Guid→string 迁移：权威字段，新客户端优先读
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
                // 既有 int64 字段已 deprecated：强制返回 0，避免 GetHashCode 失真
                SpuId = 0,
                SpuIdStr = r.SpuId.ToString(),
                Rating = r.Rating,
                Content = r.Content,
                CreatedAt = r.CreatedAt.ToString("O")  // ISO 8601
            });
        }
        return proto;
    }
}
