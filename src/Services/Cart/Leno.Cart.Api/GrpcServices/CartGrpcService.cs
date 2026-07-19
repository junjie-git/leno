using Grpc.Core;
using Leno.Cart.Application;
using Leno.SharedContracts.Grpc.Cart.V1;
using Microsoft.AspNetCore.Authorization;

namespace Leno.Cart.Api.GrpcServices;

/// <summary>
/// 购物车域 gRPC 服务端（M4 双轨方案）。
/// 复用 <see cref="ICartInternalQueryService"/> 业务逻辑，与 InternalCartsController HTTP 路径双轨。
/// 鉴权由 GrpcInternalKeyInterceptor 拦截器统一处理（metadata x-internal-key）。
/// </summary>
[Authorize]
public sealed class CartGrpcService : CartInternalService.CartInternalServiceBase
{
    private readonly ICartInternalQueryService _queryService;
    private readonly ILogger<CartGrpcService> _logger;

    public CartGrpcService(
        ICartInternalQueryService queryService,
        ILogger<CartGrpcService> logger)
    {
        _queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override async Task<CartSnapshot> GetCartSnapshot(
        GetCartSnapshotRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out var userId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"UserId 格式无效：{request.UserId}"));
        }

        var dto = await _queryService.GetCartSnapshotAsync(userId, context.CancellationToken)
            .ConfigureAwait(false);

        if (dto is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"Cart for user {request.UserId} not found"));
        }

        return MapToProto(dto);
    }

    public override async Task<CheckoutPreview> GetCheckoutPreview(
        GetCheckoutPreviewRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out var userId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"UserId 格式无效：{request.UserId}"));
        }

        var dto = await _queryService.GetCheckoutPreviewAsync(userId, context.CancellationToken)
            .ConfigureAwait(false);

        if (dto is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"Checkout preview for user {request.UserId} not found"));
        }

        return MapToProto(dto);
    }

    private static CartSnapshot MapToProto(CartSnapshotDto dto)
    {
        var proto = new CartSnapshot
        {
            CartId = dto.CartId.ToString(),
            TotalCents = dto.TotalCents
        };
        foreach (var item in dto.Items)
        {
            // 双写：既有 int64 字段（GetHashCode，向后兼容）+ 新增 string 字段（Guid.ToString()）
            proto.Items.Add(new CartItem
            {
                SkuId = (long)item.SkuId.GetHashCode(),
                SkuIdStr = item.SkuId.ToString(),
                Quantity = item.Quantity,
                UnitPriceCents = item.UnitPriceCents
            });
        }
        return proto;
    }

    private static CheckoutPreview MapToProto(CheckoutPreviewSnapshotDto dto) => new()
    {
        SubtotalCents = dto.SubtotalCents,
        DiscountCents = dto.DiscountCents,
        ShippingCents = dto.ShippingCents,
        TotalCents = dto.TotalCents
    };
}
