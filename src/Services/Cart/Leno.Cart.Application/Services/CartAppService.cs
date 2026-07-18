using Leno.Cart.Application.DTOs;
using Leno.Cart.Domain.Aggregates;
using Leno.Cart.Domain.Exceptions;
using Leno.Cart.Domain.Repositories;
using Leno.Cart.Domain.Services;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using CartAggregate = Leno.Cart.Domain.Aggregates.Cart;

namespace Leno.Cart.Application.Services;

/// <summary>
/// 购物车管理应用服务实现。
/// 通过 <see cref="ICartRepository"/> 持久化、<see cref="ICartPriceService"/> 防腐层查询实时价格。
/// </summary>
public sealed class CartAppService : ICartAppService
{
    private readonly ICartRepository _cartRepository;
    private readonly ICartPriceService _priceService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAnonymousCartRepository _anonymousCartRepository;
    private readonly ILogger<CartAppService> _logger;

    public CartAppService(
        ICartRepository cartRepository,
        ICartPriceService priceService,
        IUnitOfWork unitOfWork,
        IAnonymousCartRepository anonymousCartRepository,
        ILogger<CartAppService> logger)
    {
        ArgumentNullException.ThrowIfNull(cartRepository);
        ArgumentNullException.ThrowIfNull(priceService);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(anonymousCartRepository);
        ArgumentNullException.ThrowIfNull(logger);
        _cartRepository = cartRepository;
        _priceService = priceService;
        _unitOfWork = unitOfWork;
        _anonymousCartRepository = anonymousCartRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CartDto> AddItemAsync(Guid userId, AddCartItemDto dto, CancellationToken ct = default)
    {
        var cart = await GetOrCreateCartAsync(userId, ct);
        cart.AddItem(dto.SkuId, dto.Quantity, dto.SellerId);
        await _unitOfWork.SaveEntitiesAsync(ct);
        return await BuildCartDtoAsync(cart, ct);
    }

    /// <inheritdoc />
    public async Task<CartDto> UpdateQuantityAsync(Guid userId, Guid skuId, UpdateCartItemQuantityDto dto, CancellationToken ct = default)
    {
        var cart = await RequireCartAsync(userId, ct);
        cart.UpdateItemQuantity(skuId, dto.Quantity);
        await _unitOfWork.SaveEntitiesAsync(ct);
        return await BuildCartDtoAsync(cart, ct);
    }

    /// <inheritdoc />
    public async Task<CartDto> RemoveItemAsync(Guid userId, Guid skuId, CancellationToken ct = default)
    {
        var cart = await RequireCartAsync(userId, ct);
        cart.RemoveItem(skuId);
        await _unitOfWork.SaveEntitiesAsync(ct);
        return await BuildCartDtoAsync(cart, ct);
    }

    /// <inheritdoc />
    public async Task<CartDto> SelectItemsAsync(Guid userId, SelectCartItemsDto dto, CancellationToken ct = default)
    {
        var cart = await RequireCartAsync(userId, ct);
        if (dto.Selected)
        {
            cart.SelectItems(dto.SkuIds);
        }
        else
        {
            cart.DeselectItems(dto.SkuIds);
        }

        await _unitOfWork.SaveEntitiesAsync(ct);
        return await BuildCartDtoAsync(cart, ct);
    }

    /// <inheritdoc />
    public async Task<CartDto> ToggleAllSelectionAsync(Guid userId, bool isSelected, CancellationToken ct = default)
    {
        var cart = await RequireCartAsync(userId, ct);
        cart.ToggleAllSelection(isSelected);
        await _unitOfWork.SaveEntitiesAsync(ct);
        return await BuildCartDtoAsync(cart, ct);
    }

    /// <inheritdoc />
    public async Task<CartDto> GetCartAsync(Guid userId, CancellationToken ct = default)
    {
        var cart = await GetOrCreateCartAsync(userId, ct);
        return await BuildCartDtoAsync(cart, ct);
    }

    /// <inheritdoc />
    public async Task<CheckoutPreviewDto> PreviewCheckoutAsync(Guid userId, CancellationToken ct = default)
    {
        var cart = await RequireCartAsync(userId, ct);
        var selectedItems = cart.Items.Where(i => i.IsSelected).ToList();
        if (selectedItems.Count == 0)
        {
            return new CheckoutPreviewDto();
        }

        // 价格服务失败时直接抛出，由全局异常中间件转换为明确错误响应，阻止 0 元结算
        var priceSnapshots = await _priceService.GetSkuPricesAsync(selectedItems.Select(i => i.SkuId), ct);
        var priceMap = priceSnapshots.ToDictionary(p => p.SkuId);

        var groups = selectedItems
            .GroupBy(i => i.SellerId)
            .Select(g =>
            {
                var items = g.Select(i => BuildItemDto(i, priceMap, priceServiceUnavailable: false)).ToList();
                return new CheckoutGroupDto
                {
                    SellerId = g.Key,
                    Items = items,
                    SubtotalAmount = items.Where(i => !i.PriceUnavailable).Sum(i => i.Subtotal),
                    Currency = items.FirstOrDefault()?.Currency ?? "CNY"
                };
            })
            .ToList();

        // 价格未命中（部分 SKU 缺失）同样阻止结算，避免误导性的 0 元结算单
        if (groups.SelectMany(g => g.Items).Any(i => i.PriceUnavailable))
        {
            throw new CartDomainException("部分商品价格加载失败，暂不可结算", "CART_PRICE_UNAVAILABLE");
        }

        return new CheckoutPreviewDto
        {
            Groups = groups,
            TotalAmount = groups.Sum(g => g.SubtotalAmount),
            Currency = groups.FirstOrDefault()?.Currency ?? "CNY",
            TotalCount = selectedItems.Sum(i => i.Quantity)
        };
    }

    /// <inheritdoc />
    public async Task<CartDto> MergeAnonymousCartAsync(Guid userId, string anonymousId, CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
        {
            throw new CartDomainException("UserId 不可为空", "CART_USER_REQUIRED");
        }

        if (string.IsNullOrWhiteSpace(anonymousId))
        {
            throw new CartDomainException("匿名会话标识不可为空", "CART_ANONYMOUS_ID_REQUIRED");
        }

        // 加载匿名购物车
        var anonymousCart = await _anonymousCartRepository.GetAsync(anonymousId, ct);
        if (anonymousCart is null)
        {
            _logger.LogInformation("匿名购物车不存在或已合并，跳过合并 AnonymousId={AnonymousId}", anonymousId);
            var existingCart = await GetOrCreateCartAsync(userId, ct);
            return await BuildCartDtoAsync(existingCart, ct);
        }

        // 加载用户购物车（不存在则创建）
        var userCart = await GetOrCreateCartAsync(userId, ct);

        // 执行合并
        var mergedCount = userCart.MergeFrom(anonymousCart);

        // 收集合并领域事件，由 UnitOfWork 的发件箱经 IIntegrationEventMapper 翻译为 CartMergedEvent 集成事件对外发布
        userCart.RecordMergedEvent(anonymousId, mergedCount);

        // 保存用户购物车（含发件箱事件落库）
        await _unitOfWork.SaveEntitiesAsync(ct);

        // 删除匿名购物车
        await _anonymousCartRepository.RemoveAsync(anonymousId, ct);

        return await BuildCartDtoAsync(userCart, ct);
    }

    private async Task<CartAggregate> GetOrCreateCartAsync(Guid userId, CancellationToken ct)
    {
        if (userId == Guid.Empty)
        {
            throw new CartDomainException("UserId 不可为空", "CART_USER_REQUIRED");
        }

        var cart = await _cartRepository.GetByUserIdAsync(userId, ct);
        if (cart is null)
        {
            cart = CartAggregate.Create(Guid.NewGuid(), userId);
            await _cartRepository.AddAsync(cart, ct);
        }

        return cart;
    }

    private async Task<CartAggregate> RequireCartAsync(Guid userId, CancellationToken ct)
    {
        var cart = await _cartRepository.GetByUserIdAsync(userId, ct)
                   ?? throw new CartDomainException("购物车不存在", "CART_NOT_FOUND");
        return cart;
    }

    private async Task<CartDto> BuildCartDtoAsync(CartAggregate cart, CancellationToken ct)
    {
        var skuIds = cart.Items.Select(i => i.SkuId).Distinct().ToList();
        Dictionary<Guid, SkuPriceSnapshot> priceMap = new();
        var priceServiceUnavailable = false;

        if (skuIds.Count > 0)
        {
            try
            {
                var priceSnapshots = await _priceService.GetSkuPricesAsync(skuIds, ct);
                priceMap = priceSnapshots.ToDictionary(p => p.SkuId);
            }
            catch (CartDomainException ex)
            {
                // 购物车"查看"场景不因价格服务故障整体崩溃，降级展示并标记 PriceUnavailable，
                // 由前端禁止结算；详见 PreviewCheckoutAsync 对结算的硬性拦截。
                _logger.LogWarning(ex, "购物车价格服务不可用，降级展示 UserId={UserId} ItemCount={ItemCount}",
                    cart.UserId, skuIds.Count);
                priceServiceUnavailable = true;
            }
        }

        var itemDtos = cart.Items
            .Select(i => BuildItemDto(i, priceMap, priceServiceUnavailable))
            .ToList();

        // 选中项总金额仅累计价格可用项，避免把价格加载失败项以 0 元计入可结算金额
        var selectedTotalAmount = itemDtos
            .Where(i => i.IsSelected && !i.PriceUnavailable)
            .Sum(i => i.Subtotal);

        return new CartDto
        {
            Id = cart.Id,
            UserId = cart.UserId,
            Items = itemDtos,
            SelectedTotalAmount = selectedTotalAmount,
            Currency = itemDtos.FirstOrDefault()?.Currency ?? "CNY",
            TotalCount = itemDtos.Sum(i => i.Quantity)
        };
    }

    private static CartItemDto BuildItemDto(
        CartItem item,
        Dictionary<Guid, SkuPriceSnapshot> priceMap,
        bool priceServiceUnavailable)
    {
        // 价格服务整体不可用，或单 SKU 未命中快照：标记 PriceUnavailable，避免误导性 0 元可结算
        if (priceServiceUnavailable || !priceMap.TryGetValue(item.SkuId, out var snapshot))
        {
            return new CartItemDto
            {
                Id = item.Id,
                SkuId = item.SkuId,
                SellerId = item.SellerId,
                Quantity = item.Quantity,
                IsSelected = item.IsSelected,
                SourceCartItemId = item.SourceCartItemId,
                UnitPrice = 0,
                Currency = "CNY",
                Title = "[价格加载失败]",
                MainImageUrl = string.Empty,
                Available = false,
                PriceUnavailable = true
            };
        }

        return new CartItemDto
        {
            Id = item.Id,
            SkuId = item.SkuId,
            SellerId = item.SellerId,
            Quantity = item.Quantity,
            IsSelected = item.IsSelected,
            SourceCartItemId = item.SourceCartItemId,
            UnitPrice = snapshot!.Price,
            Currency = snapshot!.Currency,
            Title = snapshot!.Title,
            MainImageUrl = snapshot!.MainImageUrl,
            Available = snapshot!.Available,
            PriceUnavailable = false
        };
    }
}
