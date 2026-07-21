using Grpc.Core;
using Leno.Promotion.Application;
using Leno.SharedContracts.Grpc.Promotion.V1;
using Microsoft.AspNetCore.Authorization;

namespace Leno.Promotion.Api.GrpcServices;

/// <summary>
/// 促销域 gRPC 服务端（M4 双轨方案）。
/// 复用 <see cref="IPromotionCalculateAppService"/> 与 <see cref="ICouponAppService"/> 应用层逻辑，
/// 与 InternalPromotionController HTTP 路径双轨。
/// 鉴权由 GrpcInternalKeyInterceptor 拦截器统一处理（metadata x-internal-key）。
/// </summary>
[Authorize]
public sealed class PromotionGrpcService : PromotionInternalService.PromotionInternalServiceBase
{
    private readonly IPromotionCalculateAppService _calculateService;
    private readonly ICouponAppService _couponAppService;
    private readonly ILogger<PromotionGrpcService> _logger;

    public PromotionGrpcService(
        IPromotionCalculateAppService calculateService,
        ICouponAppService couponAppService,
        ILogger<PromotionGrpcService> logger)
    {
        _calculateService = calculateService;
        _couponAppService = couponAppService;
        _logger = logger;
    }

    public override async Task<CalculateDiscountResponse> CalculateDiscount(
        CalculateDiscountRequest request, ServerCallContext context)
    {
        // OrderItem.sku_id 优先读 string（Guid.ToString()），回退到 int64（向后兼容旧客户端）
        // 注：int64→Guid 反向不可靠（GetHashCode 单向），回退时仅用 Guid.Empty 占位
        var input = new CalculateDiscountDto
        {
            UserId = new Guid(request.UserId),
            Items = request.Items.Select(i => new DiscountItemInput
            {
                SkuId = !string.IsNullOrEmpty(i.SkuIdStr)
                    ? Guid.Parse(i.SkuIdStr)
                    : Guid.Empty,
                Subtotal = i.SubtotalCents / 100m
            }).ToList()
        };

        var result = await _calculateService.CalculateDiscountAsync(input, context.CancellationToken)
            .ConfigureAwait(false);

        return new CalculateDiscountResponse
        {
            DiscountCents = (long)(result.TotalDiscountAmount * 100)
        };
    }

    public override async Task<LockCouponResponse> LockCoupon(
        LockCouponRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out var userId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"Invalid user_id: {request.UserId}"));
        }
        if (!Guid.TryParse(request.CouponId, out var couponId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"Invalid coupon_id: {request.CouponId}"));
        }
        if (!Guid.TryParse(request.OrderId, out var orderId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"Invalid order_id: {request.OrderId}"));
        }

        await _couponAppService.LockCouponAsync(userId, couponId, orderId, context.CancellationToken)
            .ConfigureAwait(false);
        return new LockCouponResponse { Success = true };
    }

    public override async Task<ReleaseCouponsResponse> ReleaseCoupons(
        ReleaseCouponsRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.OrderId, out var orderId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"Invalid order_id: {request.OrderId}"));
        }

        await _couponAppService.ReleaseCouponsAsync(orderId, context.CancellationToken)
            .ConfigureAwait(false);
        return new ReleaseCouponsResponse { Success = true };
    }

    public override async Task<CouponInfo> GetCouponInfo(GetCouponInfoRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.CouponId, out var couponId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"Invalid coupon id: {request.CouponId}"));
        }

        var dto = await _couponAppService.GetByIdAsync(couponId, context.CancellationToken)
            .ConfigureAwait(false);

        if (dto is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"Coupon {request.CouponId} not found"));
        }

        return new CouponInfo
        {
            CouponId = dto.Id.ToString(),
            Title = dto.Name,
            DiscountCents = (long)(dto.FaceValue * 100),
            Status = dto.Status.ToString()
        };
    }
}
