using Leno.SellerShop.Application.DTOs;
using Leno.SellerShop.Application.Services;
using Leno.SellerShop.Domain.Exceptions;
using Microsoft.Extensions.Logging;

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
    private readonly IProductAntiCorruptionService _productAntiCorruption;
    private readonly IOrderAntiCorruptionService _orderAntiCorruption;
    private readonly ILogger<SellerInternalQueryService> _logger;

    public SellerInternalQueryService(
        ISellerAppService sellerAppService,
        IShopAppService shopAppService,
        IProductAntiCorruptionService productAntiCorruption,
        IOrderAntiCorruptionService orderAntiCorruption,
        ILogger<SellerInternalQueryService> logger)
    {
        _sellerAppService = sellerAppService ?? throw new ArgumentNullException(nameof(sellerAppService));
        _shopAppService = shopAppService ?? throw new ArgumentNullException(nameof(shopAppService));
        _productAntiCorruption = productAntiCorruption ?? throw new ArgumentNullException(nameof(productAntiCorruption));
        _orderAntiCorruption = orderAntiCorruption ?? throw new ArgumentNullException(nameof(orderAntiCorruption));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

    /// <inheritdoc />
    public async Task<bool> ValidateOwnershipAsync(
        Guid sellerId, string resourceType, Guid resourceId, CancellationToken ct = default)
    {
        return resourceType switch
        {
            "shop" => await ValidateShopOwnershipAsync(sellerId, resourceId, ct).ConfigureAwait(false),
            "spu" => await ValidateSpuOwnershipAsync(sellerId, resourceId, ct).ConfigureAwait(false),
            "order" => await ValidateOrderOwnershipAsync(sellerId, resourceId, ct).ConfigureAwait(false),
            _ => LogUnknownResourceType(resourceType)
        };
    }

    private async Task<bool> ValidateShopOwnershipAsync(Guid sellerId, Guid shopId, CancellationToken ct)
    {
        // 卖家未关联店铺时 IShopAppService.GetMyShopAsync 抛 SHOP_NOT_FOUND，
        // fail-closed 返回 false（资源不存在即不归属）。
        ShopDto shop;
        try
        {
            shop = await _shopAppService.GetMyShopAsync(sellerId, ct).ConfigureAwait(false);
        }
        catch (SellerShopDomainException ex) when (ex.ErrorCode == "SHOP_NOT_FOUND")
        {
            return false;
        }

        return shop.Id == shopId;
    }

    private async Task<bool> ValidateSpuOwnershipAsync(Guid sellerId, Guid spuId, CancellationToken ct)
    {
        // 防腐层失败时返回 null（fail-closed），由本方法判 false，避免跨域故障阻断卖家操作。
        var spuSellerId = await _productAntiCorruption.GetSpuSellerIdAsync(spuId, ct).ConfigureAwait(false);
        return spuSellerId.HasValue && spuSellerId.Value == sellerId;
    }

    private async Task<bool> ValidateOrderOwnershipAsync(Guid sellerId, Guid orderId, CancellationToken ct)
    {
        // 防腐层失败时返回 null（fail-closed），由本方法判 false，避免跨域故障阻断卖家操作。
        var orderSellerId = await _orderAntiCorruption.GetOrderSellerIdAsync(orderId, ct).ConfigureAwait(false);
        return orderSellerId.HasValue && orderSellerId.Value == sellerId;
    }

    private bool LogUnknownResourceType(string resourceType)
    {
        _logger.LogWarning("未知 resource_type: {ResourceType}", resourceType);
        return false;
    }
}
