using Leno.SellerShop.Application.DTOs;
using Leno.SellerShop.Application.Services;
using Leno.SellerShop.Domain.Exceptions;

namespace Leno.SellerShop.Application.InternalQueryServices;

/// <summary>
/// 卖家店铺域跨 BC 内部查询服务实现（M4 双轨方案）。
/// 委托 <see cref="ISellerAppService"/> 与 <see cref="IShopAppService"/> 的既有查询方法，映射为跨 BC DTO。
/// 既有 AppService 在资源不存在时抛 <see cref="SellerShopDomainException"/>，本实现捕获后返回 null，
/// 便于 GrpcService 层统一映射为 gRPC NotFound 状态码。
/// </summary>
public sealed class SellerInternalQueryService : ISellerInternalQueryService
{
    private readonly ISellerAppService _sellerAppService;
    private readonly IShopAppService _shopAppService;

    public SellerInternalQueryService(
        ISellerAppService sellerAppService,
        IShopAppService shopAppService)
    {
        _sellerAppService = sellerAppService ?? throw new ArgumentNullException(nameof(sellerAppService));
        _shopAppService = shopAppService ?? throw new ArgumentNullException(nameof(shopAppService));
    }

    /// <inheritdoc />
    public async Task<SellerInfoDto?> GetSellerInfoAsync(Guid sellerId, CancellationToken ct = default)
    {
        SellerProfileDto seller;
        try
        {
            seller = await _sellerAppService.GetSellerProfileAsync(sellerId, ct);
        }
        catch (SellerShopDomainException ex) when (ex.ErrorCode == "SELLER_NOT_FOUND")
        {
            return null;
        }

        // SellerProfileDto 未携带 ShopId，通过 IShopAppService.GetMyShopAsync 反查；
        // 若卖家尚未创建店铺，ShopId 保持 Guid.Empty。
        Guid shopId = Guid.Empty;
        try
        {
            var shop = await _shopAppService.GetMyShopAsync(sellerId, ct);
            shopId = shop.Id;
        }
        catch (SellerShopDomainException ex) when (ex.ErrorCode == "SHOP_NOT_FOUND")
        {
            // 卖家档案存在但未关联店铺，ShopId 留空
        }

        return new SellerInfoDto
        {
            SellerId = seller.UserId,
            Name = seller.RealName,
            Status = seller.Status.ToString(),
            ShopId = shopId
        };
    }

    /// <inheritdoc />
    public async Task<ShopInfoDto?> GetShopInfoAsync(Guid shopId, CancellationToken ct = default)
    {
        ShopDto shop;
        try
        {
            shop = await _shopAppService.GetShopInfoAsync(shopId, ct);
        }
        catch (SellerShopDomainException ex) when (ex.ErrorCode == "SHOP_NOT_FOUND")
        {
            return null;
        }

        return new ShopInfoDto
        {
            ShopId = shop.Id,
            Name = shop.ShopName,
            Status = shop.Status.ToString(),
            SellerId = shop.SellerId
        };
    }
}
