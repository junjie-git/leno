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
        // proto shop_id 是 int64，业务侧用 Guid。
        // POC 简化：将 int64 嵌入 Guid 前 4 字节，其余补零（生产化改为 proto 字段改 string）。
        var shopId = new Guid((int)request.ShopId, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        var dto = await _queryService.GetShopInfoAsync(shopId, context.CancellationToken)
            .ConfigureAwait(false);

        if (dto is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"Shop {request.ShopId} not found"));
        }

        return MapToProto(dto);
    }

    public override Task<ValidateSellerOwnershipResponse> ValidateSellerOwnership(
        ValidateSellerOwnershipRequest request, ServerCallContext context)
    {
        // F1.4 独立任务，本次抛 Unimplemented
        throw new RpcException(new Status(StatusCode.Unimplemented,
            "ValidateSellerOwnership not implemented, see F1.4"));
    }

    private static SellerInfo MapToProto(SellerInfoDto dto) => new()
    {
        SellerId = dto.SellerId.ToString(),
        Name = dto.Name,
        Status = dto.Status,
        // POC 简化：Guid→int64 不可逆映射，生产化改为 proto 字段改 string
        ShopId = (long)dto.ShopId.GetHashCode()
    };

    private static ShopInfo MapToProto(ShopInfoDto dto) => new()
    {
        // POC 简化：Guid→int64 不可逆映射，生产化改为 proto 字段改 string
        ShopId = (long)dto.ShopId.GetHashCode(),
        Name = dto.Name,
        Status = dto.Status,
        SellerId = dto.SellerId.ToString()
    };
}
