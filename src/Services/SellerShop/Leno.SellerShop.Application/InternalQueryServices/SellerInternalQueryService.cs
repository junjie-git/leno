using Leno.SellerShop.Application.DTOs;
using Leno.SellerShop.Application.Services;
using Microsoft.Extensions.Logging;

namespace Leno.SellerShop.Application.InternalQueryServices;

/// <summary>
/// 卖家店铺域跨 BC 内部查询服务实现（M4 双轨方案）。
/// 委托 <see cref="ISellerAppService"/> 与 <see cref="IShopAppService"/> 的 TryGet 查询方法，映射为跨 BC DTO。
/// 资源不存在时返回 null，便于 GrpcService 层统一映射为 gRPC NotFound 状态码。
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
        var seller = await _sellerAppService.TryGetSellerProfileAsync(sellerId, ct);
        if (seller is null)
        {
            return null;
        }

        // SellerProfileDto 未携带 ShopId，通过 TryGetShopBySellerIdAsync 反查；
        // 若卖家尚未创建店铺，ShopId 保持 Guid.Empty。
        var shop = await _shopAppService.TryGetShopBySellerIdAsync(sellerId, ct);

        return new SellerInfoDto
        {
            SellerId = seller.UserId,
            Name = seller.RealName,
            Status = seller.Status.ToString(),
            ShopId = shop?.Id ?? Guid.Empty
        };
    }

    /// <inheritdoc />
    public async Task<ShopInfoDto?> GetShopInfoAsync(Guid shopId, CancellationToken ct = default)
    {
        var shop = await _shopAppService.TryGetShopByIdAsync(shopId, ct);
        if (shop is null)
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
        // 卖家未关联店铺时 TryGetShopBySellerIdAsync 返回 null，
        // fail-closed 返回 false（资源不存在即不归属）。
        var shop = await _shopAppService.TryGetShopBySellerIdAsync(sellerId, ct).ConfigureAwait(false);
        return shop is not null && shop.Id == shopId;
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
