using Leno.Cart.Application.DTOs;
using Leno.Cart.Domain.Aggregates;
using Leno.Cart.Domain.Exceptions;
using Leno.Cart.Domain.Repositories;
using Leno.Cart.Domain.Services;
using Leno.SharedContracts.Events;
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
    private readonly IEventBus _eventBus;
    private readonly ILogger<CartAppService> _logger;

    public CartAppService(
        ICartRepository cartRepository,
        ICartPriceService priceService,
        IUnitOfWork unitOfWork,
        IAnonymousCartRepository anonymousCartRepository,
        IEventBus eventBus,
        ILogger<CartAppService> logger)
    {
        ArgumentNullException.ThrowIfNull(cartRepository);
        ArgumentNullException.ThrowIfNull(priceService);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(anonymousCartRepository);
        ArgumentNullException.ThrowIfNull(eventBus);
        ArgumentNullException.ThrowIfNull(logger);
        _cartRepository = cartRepository;
        _priceService = priceService;
        _unitOfWork = unitOfWork;
        _anonymousCartRepository = anonymousCartRepository;
        _eventBus = eventBus;
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

        var priceSnapshots = await _priceService.GetSkuPricesAsync(selectedItems.Select(i => i.SkuId), ct);
        var priceMap = priceSnapshots.ToDictionary(p => p.SkuId);

        var groups = selectedItems
            .GroupBy(i => i.SellerId)
            .Select(g =>
            {
                var items = g.Select(i => BuildItemDto(i, priceMap)).ToList();
                return new CheckoutGroupDto
                {
                    SellerId = g.Key,
                    Items = items,
                    SubtotalAmount = items.Sum(i => i.Subtotal),
                    Currency = items.FirstOrDefault()?.Currency ?? "CNY"
                };
            })
            .ToList();

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
            throw new CartDomainException("UserId 不可为空", "CART_USER_REQUIRED", 401);
        }

        if (string.IsNullOrWhiteSpace(anonymousId))
        {
            throw new CartDomainException("匿名会话标识不可为空", "CART_ANONYMOUS_ID_REQUIRED", 400);
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

        // 保存用户购物车
        await _unitOfWork.SaveEntitiesAsync(ct);

        // 删除匿名购物车
        await _anonymousCartRepository.RemoveAsync(anonymousId, ct);

        // 发布合并事件
        await _eventBus.PublishAsync(new CartMergedEvent(userId, anonymousId, mergedCount), ct);

        return await BuildCartDtoAsync(userCart, ct);
    }

    private async Task<CartAggregate> GetOrCreateCartAsync(Guid userId, CancellationToken ct)
    {
        if (userId == Guid.Empty)
        {
            throw new CartDomainException("UserId 不可为空", "CART_USER_REQUIRED", 401);
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
                   ?? throw new CartDomainException("购物车不存在", "CART_NOT_FOUND", 404);
        return cart;
    }

    private async Task<CartDto> BuildCartDtoAsync(CartAggregate cart, CancellationToken ct)
    {
        var skuIds = cart.Items.Select(i => i.SkuId).Distinct().ToList();
        var priceSnapshots = skuIds.Count > 0
            ? await _priceService.GetSkuPricesAsync(skuIds, ct)
            : Array.Empty<SkuPriceSnapshot>();
        var priceMap = priceSnapshots.ToDictionary(p => p.SkuId);

        var itemDtos = cart.Items
            .Select(i => BuildItemDto(i, priceMap))
            .ToList();

        return new CartDto
        {
            Id = cart.Id,
            UserId = cart.UserId,
            Items = itemDtos,
            SelectedTotalAmount = itemDtos.Where(i => i.IsSelected).Sum(i => i.Subtotal),
            Currency = itemDtos.FirstOrDefault()?.Currency ?? "CNY",
            TotalCount = itemDtos.Sum(i => i.Quantity)
        };
    }

    private static CartItemDto BuildItemDto(CartItem item, Dictionary<Guid, SkuPriceSnapshot> priceMap)
    {
        var hasPrice = priceMap.TryGetValue(item.SkuId, out var snapshot);
        return new CartItemDto
        {
            Id = item.Id,
            SkuId = item.SkuId,
            SellerId = item.SellerId,
            Quantity = item.Quantity,
            IsSelected = item.IsSelected,
            SourceCartItemId = item.SourceCartItemId,
            UnitPrice = hasPrice ? snapshot!.Price : 0,
            Currency = hasPrice ? snapshot!.Currency : "CNY",
            Title = hasPrice ? snapshot!.Title : string.Empty,
            MainImageUrl = hasPrice ? snapshot!.MainImageUrl : string.Empty,
            Available = hasPrice && snapshot!.Available
        };
    }
}
