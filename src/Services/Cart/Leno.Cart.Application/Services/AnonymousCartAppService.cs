using Leno.Cart.Application.DTOs;
using Leno.Cart.Domain.Aggregates;
using Leno.Cart.Domain.Exceptions;
using Leno.Cart.Domain.Repositories;
using Leno.Cart.Domain.Services;
using CartAggregate = Leno.Cart.Domain.Aggregates.Cart;

namespace Leno.Cart.Application.Services;

/// <summary>
/// 匿名购物车管理应用服务实现。
/// 通过 <see cref="IAnonymousCartRepository"/> 持久化至 Redis、<see cref="ICartPriceService"/> 防腐层查询实时价格。
/// </summary>
public sealed class AnonymousCartAppService : IAnonymousCartAppService
{
    private readonly IAnonymousCartRepository _cartRepository;
    private readonly ICartPriceService _priceService;

    public AnonymousCartAppService(
        IAnonymousCartRepository cartRepository,
        ICartPriceService priceService)
    {
        ArgumentNullException.ThrowIfNull(cartRepository);
        ArgumentNullException.ThrowIfNull(priceService);
        _cartRepository = cartRepository;
        _priceService = priceService;
    }

    /// <inheritdoc />
    public async Task<AnonymousCartResponseDto> CreateCartAsync(CancellationToken ct = default)
    {
        var sessionId = Guid.NewGuid().ToString("N");
        var cart = CartAggregate.CreateAnonymous(Guid.NewGuid());
        await _cartRepository.SaveAsync(sessionId, cart, ct);
        var cartDto = await BuildCartDtoAsync(cart, ct);
        return new AnonymousCartResponseDto
        {
            SessionId = sessionId,
            Cart = cartDto
        };
    }

    /// <inheritdoc />
    public async Task<CartDto> GetCartAsync(string sessionId, CancellationToken ct = default)
    {
        var cart = await GetOrCreateCartAsync(sessionId, ct);
        await _cartRepository.RefreshTtlAsync(sessionId, ct);
        return await BuildCartDtoAsync(cart, ct);
    }

    /// <inheritdoc />
    public async Task<CartDto> AddItemAsync(string sessionId, AddCartItemDto dto, CancellationToken ct = default)
    {
        var cart = await GetOrCreateCartAsync(sessionId, ct);
        cart.AddItem(dto.SkuId, dto.Quantity, dto.SellerId);
        await _cartRepository.SaveAsync(sessionId, cart, ct);
        await _cartRepository.RefreshTtlAsync(sessionId, ct);
        return await BuildCartDtoAsync(cart, ct);
    }

    /// <inheritdoc />
    public async Task<CartDto> UpdateQuantityAsync(string sessionId, Guid skuId, UpdateCartItemQuantityDto dto, CancellationToken ct = default)
    {
        var cart = await RequireCartAsync(sessionId, ct);
        cart.UpdateItemQuantity(skuId, dto.Quantity);
        await _cartRepository.SaveAsync(sessionId, cart, ct);
        await _cartRepository.RefreshTtlAsync(sessionId, ct);
        return await BuildCartDtoAsync(cart, ct);
    }

    /// <inheritdoc />
    public async Task<CartDto> RemoveItemAsync(string sessionId, Guid skuId, CancellationToken ct = default)
    {
        var cart = await RequireCartAsync(sessionId, ct);
        cart.RemoveItem(skuId);
        await _cartRepository.SaveAsync(sessionId, cart, ct);
        await _cartRepository.RefreshTtlAsync(sessionId, ct);
        return await BuildCartDtoAsync(cart, ct);
    }

    /// <inheritdoc />
    public async Task<CartDto> SelectItemsAsync(string sessionId, SelectCartItemsDto dto, CancellationToken ct = default)
    {
        var cart = await RequireCartAsync(sessionId, ct);
        if (dto.Selected)
        {
            cart.SelectItems(dto.SkuIds);
        }
        else
        {
            cart.DeselectItems(dto.SkuIds);
        }

        await _cartRepository.SaveAsync(sessionId, cart, ct);
        await _cartRepository.RefreshTtlAsync(sessionId, ct);
        return await BuildCartDtoAsync(cart, ct);
    }

    /// <inheritdoc />
    public async Task<CheckoutPreviewDto> PreviewCheckoutAsync(string sessionId, CancellationToken ct = default)
    {
        var cart = await RequireCartAsync(sessionId, ct);
        await _cartRepository.RefreshTtlAsync(sessionId, ct);
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

    private async Task<CartAggregate> GetOrCreateCartAsync(string sessionId, CancellationToken ct)
    {
        var cart = await _cartRepository.GetAsync(sessionId, ct);
        if (cart is null)
        {
            cart = CartAggregate.CreateAnonymous(Guid.NewGuid());
            await _cartRepository.SaveAsync(sessionId, cart, ct);
        }

        return cart;
    }

    private async Task<CartAggregate> RequireCartAsync(string sessionId, CancellationToken ct)
    {
        var cart = await _cartRepository.GetAsync(sessionId, ct)
                   ?? throw new CartDomainException("匿名购物车不存在", "CART_NOT_FOUND");
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