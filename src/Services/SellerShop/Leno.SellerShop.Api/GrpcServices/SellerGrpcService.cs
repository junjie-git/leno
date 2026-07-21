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
        // 优先读 string 字段（Guid.ToString()），回退到 int64（向后兼容旧客户端）
        Guid shopId;
        if (!string.IsNullOrEmpty(request.ShopIdStr))
        {
            if (!Guid.TryParse(request.ShopIdStr, out shopId))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, $"Invalid shop_id_str: {request.ShopIdStr}"));
            }
        }
        else
        {
            // 旧客户端回退：将 int64 嵌入 Guid 前 4 字节，其余补零
            shopId = new Guid((int)request.ShopId, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        }

        var dto = await _queryService.GetShopInfoAsync(shopId, context.CancellationToken)
            .ConfigureAwait(false);

        if (dto is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"Shop {request.ShopId} not found"));
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
        // deprecated：int64 字段保留固定值 0，不再使用 Guid.GetHashCode() 不可逆映射（存在哈希冲突且不可逆）
        ShopId = 0L,
        // 新增 string 字段（Guid→string 迁移，新客户端优先读 shop_id_str）
        ShopIdStr = dto.ShopId.ToString()
    };

    private static ShopInfo MapToProto(ShopInfoDto dto) => new()
    {
        // deprecated：int64 字段保留固定值 0，不再使用 GetHashCode
        ShopId = 0L,
        Name = dto.Name,
        Status = dto.Status,
        SellerId = dto.SellerId.ToString(),
        // string 字段（Guid→string 迁移，新客户端优先读）
        ShopIdStr = dto.ShopId.ToString()
    };
}
