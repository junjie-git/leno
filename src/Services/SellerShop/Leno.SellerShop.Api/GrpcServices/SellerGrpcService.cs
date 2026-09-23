using Grpc.Core;
using Leno.SellerShop.Application;
using Leno.SharedContracts.Grpc.Seller.V1;
using Microsoft.AspNetCore.Authorization;

namespace Leno.SellerShop.Api.GrpcServices;

/// <summary>
/// 卖家店铺域 gRPC 服务端（M4 双轨方案）。
/// 复用 <see cref="ISellerInternalQueryService"/> 业务逻辑，与 InternalSellersController/InternalShopsController HTTP 路径双轨。
/// 鉴权由 GrpcInternalKeyInterceptor 拦截器统一处理（metadata x-internal-key）。
/// </summary>
[Authorize]
public sealed class SellerGrpcService : SellerInternalService.SellerInternalServiceBase
{
    private readonly ISellerInternalQueryService _queryService;
    private readonly ILogger<SellerGrpcService> _logger;

    public SellerGrpcService(
        ISellerInternalQueryService queryService,
        ILogger<SellerGrpcService> logger)
    {
        _queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override async Task<SellerInfo> GetSellerInfo(
        GetSellerInfoRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.SellerId, out var sellerId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"Invalid seller_id: {request.SellerId}"));
        }

        var dto = await _queryService.GetSellerInfoAsync(sellerId, context.CancellationToken)
            .ConfigureAwait(false);

        if (dto is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"Seller {request.SellerId} not found"));
        }

        return MapToProto(dto);
    }

    public override async Task<ShopInfo> GetShopInfo(
        GetShopInfoRequest request, ServerCallContext context)
    {
        // C3（2026-09-24）：int64 兼容字段已从契约删除，shop_id_str 为唯一标识形态
        if (!Guid.TryParse(request.ShopIdStr, out var shopId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"Invalid or missing shop_id_str: {request.ShopIdStr}"));
        }

        var dto = await _queryService.GetShopInfoAsync(shopId, context.CancellationToken)
            .ConfigureAwait(false);

        if (dto is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"Shop {request.ShopIdStr} not found"));
        }

        return MapToProto(dto);
    }

    public override async Task<ValidateSellerOwnershipResponse> ValidateSellerOwnership(
        ValidateSellerOwnershipRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.SellerId, out var sellerId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"Invalid seller_id: {request.SellerId}"));
        }
        if (!Guid.TryParse(request.ResourceId, out var resourceId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"Invalid resource_id: {request.ResourceId}"));
        }

        var isValid = await _queryService.ValidateOwnershipAsync(
            sellerId, request.ResourceType, resourceId, context.CancellationToken)
            .ConfigureAwait(false);
        return new ValidateSellerOwnershipResponse { IsValid = isValid };
    }

    private static SellerInfo MapToProto(SellerInfoDto dto) => new()
    {
        SellerId = dto.SellerId.ToString(),
        Name = dto.Name,
        Status = dto.Status,
        // Guid→string 迁移权威字段（C3 后 int64 字段已从契约删除）
        ShopIdStr = dto.ShopId.ToString()
    };

    private static ShopInfo MapToProto(ShopInfoDto dto) => new()
    {
        Name = dto.Name,
        Status = dto.Status,
        SellerId = dto.SellerId.ToString(),
        ShopIdStr = dto.ShopId.ToString()
    };
}
