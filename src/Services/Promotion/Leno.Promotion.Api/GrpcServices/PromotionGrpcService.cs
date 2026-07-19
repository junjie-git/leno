using Grpc.Core;
using Leno.Promotion.Application;
using Leno.Promotion.Domain.Repositories;
using Leno.SharedContracts.Grpc.Promotion.V1;
using Microsoft.AspNetCore.Authorization;

namespace Leno.Promotion.Api.GrpcServices;

/// <summary>
/// 促销域 gRPC 服务端（M4 双轨方案）。
/// 复用 <see cref="IPromotionCalculateAppService"/> 与 <see cref="ICouponRepository"/> 业务逻辑，
/// 与 InternalPromotionController HTTP 路径双轨。
/// 鉴权由 GrpcInternalKeyInterceptor 拦截器统一处理（metadata x-internal-key）。
/// </summary>
[Authorize]
public sealed class PromotionGrpcService : PromotionInternalService.PromotionInternalServiceBase
{
    private readonly IPromotionCalculateAppService _calculateService;
    private readonly ICouponRepository _couponRepository;
    private readonly ILogger<PromotionGrpcService> _logger;

    public PromotionGrpcService(
        IPromotionCalculateAppService calculateService,
        ICouponRepository couponRepository,
        ILogger<PromotionGrpcService> logger)
    {
        _calculateService = calculateService;
        _couponRepository = couponRepository;
        _logger = logger;
    }

    public override async Task<CalculateDiscountResponse> CalculateDiscount(
        CalculateDiscountRequest request, ServerCallContext context)
    {
        // POC 简化：proto 中 sku_id 为 int64，无法承载 Guid，使用 Guid.Empty 占位
        // 生产化阶段需将 .proto 改为 string sku_id 承载 Guid.ToString()
        var input = new CalculateDiscountDto
        {
            UserId = new Guid(request.UserId),
            Items = request.Items.Select(i => new DiscountItemInput
            {
                SkuId = Guid.Empty,
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

    public override Task<LockCouponResponse> LockCoupon(LockCouponRequest request, ServerCallContext context)
    {
        // POC 阶段未实现券锁定逻辑（需 UserCoupon 仓储），返回 success=true 占位
        return Task.FromResult(new LockCouponResponse { Success = true });
    }

    public override Task<ReleaseCouponsResponse> ReleaseCoupons(ReleaseCouponsRequest request, ServerCallContext context)
    {
        // POC 阶段未实现券释放逻辑（需 UserCoupon 仓储），返回 success=true 占位
        return Task.FromResult(new ReleaseCouponsResponse { Success = true });
    }

    public override async Task<CouponInfo> GetCouponInfo(GetCouponInfoRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.CouponId, out var couponId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"Invalid coupon id: {request.CouponId}"));
        }

        var coupon = await _couponRepository.GetByIdAsync(couponId, context.CancellationToken)
            .ConfigureAwait(false);

        if (coupon is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"Coupon {request.CouponId} not found"));
        }

        return new CouponInfo
        {
            CouponId = coupon.Id.ToString(),
            Title = coupon.Name,
            DiscountCents = (long)(coupon.FaceValue * 100),
            Status = coupon.Status.ToString()
        };
    }
}
