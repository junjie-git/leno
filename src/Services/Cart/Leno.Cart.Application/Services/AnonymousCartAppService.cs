using Leno.Cart.Application.DTOs;
using Leno.Cart.Domain.Aggregates;
using Leno.Cart.Domain.Exceptions;
using Leno.Cart.Domain.Repositories;
using Leno.Cart.Domain.Services;
using Leno.SharedKernel.Exceptions;
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
        // P2-8：读操作不刷新 TTL，避免攻击者定时 GET 让匿名购物车永久驻留；
        // 仅写操作（Add/Update/Remove/Select/Preview）刷新 TTL，鼓励用户活跃操作。
        var cart = await GetOrCreateCartAsync(sessionId, ct);
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
                var items = g.Select(i => BuildItemDto(i, priceMap, priceServiceUnavailable: false)).ToList();
                return new CheckoutGroupDto
                {
                    SellerId = g.Key,
                    Items = items,
                    // 与 CartAppService 对齐：仅累计价格可用项
                    SubtotalAmount = items.Where(i => !i.PriceUnavailable).Sum(i => i.Subtotal),
                    Currency = items.FirstOrDefault()?.Currency ?? "CNY"
                };
            })
            .ToList();

        // 与 CartAppService 对齐：缺价项硬拦截，避免 0 元结算单
        if (groups.SelectMany(g => g.Items).Any(i => i.PriceUnavailable))
        {
            throw new CartDomainException("部分商品价格加载失败，暂不可结算", "CART_PRICE_UNAVAILABLE");
        }

        // P1-14：按币种分组聚合小计，混币种场景抛 CART_MIXED_CURRENCY 阻止结算
        var subtotalsByCurrency = groups
            .SelectMany(g => g.Items)
            .GroupBy(i => i.Currency)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Subtotal));

        if (subtotalsByCurrency.Count > 1)
        {
            throw new CartDomainException(
                "选中项包含多种币种，暂不支持跨币种合并结算，请按币种拆分下单",
                "CART_MIXED_CURRENCY");
        }

        var singleCurrency = subtotalsByCurrency.Count == 1
            ? subtotalsByCurrency.Single()
            : new KeyValuePair<string, decimal>("CNY", 0m);

        return new CheckoutPreviewDto
        {
            Groups = groups,
            TotalAmount = singleCurrency.Value,
            Currency = singleCurrency.Key,
            TotalCount = selectedItems.Sum(i => i.Quantity),
            SubtotalsByCurrency = subtotalsByCurrency
        };
    }

    private async Task<CartAggregate> GetOrCreateCartAsync(string sessionId, CancellationToken ct)
    {
        var cart = await _cartRepository.GetAsync(sessionId, ct);
        if (cart is null)
        {
            // P2-10：使用 Redis SET NX 原子创建，避免并发请求同时遇 null 都创建并覆盖后者丢失。
            // TrySaveAsync 返回 false 表示并发请求已创建，重新读取已存在的购物车。
            cart = CartAggregate.CreateAnonymous(Guid.NewGuid());
            var created = await _cartRepository.TrySaveAsync(sessionId, cart, ct);
            if (!created)
            {
                // 并发请求已写入，重新读取以获取已存在的购物车
                var existing = await _cartRepository.GetAsync(sessionId, ct);
                if (existing is not null)
                {
                    cart = existing;
                }
                // 极端情况：并发删除后仍为 null，回退使用本次创建的空购物车（无业务损失）
            }
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
        Dictionary<Guid, SkuPriceSnapshot> priceMap = new();
        var priceServiceUnavailable = false;

        if (skuIds.Count > 0)
        {
            try
            {
                var priceSnapshots = await _priceService.GetSkuPricesAsync(skuIds, ct);
                priceMap = priceSnapshots.ToDictionary(p => p.SkuId);
            }
            catch (DomainException ex)
            {
                // 与 CartAppService 对齐：防腐层抛 AntiCorruptionException（继承 DomainException）时进入降级分支
                priceServiceUnavailable = true;
            }
        }

        var itemDtos = cart.Items
            .Select(i => BuildItemDto(i, priceMap, priceServiceUnavailable))
            .ToList();

        // P1-14：与 CartAppService 对齐，按币种分组聚合选中且价格可用项的小计。
        // 单币种时 SelectedTotalAmount 与 SubtotalsByCurrency 唯一条目一致；
        // 混币种时 SelectedTotalAmount 置 0（前端按 SubtotalsByCurrency 分别展示）。
        var subtotalsByCurrency = itemDtos
            .Where(i => i.IsSelected && !i.PriceUnavailable)
            .GroupBy(i => i.Currency)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Subtotal));

        decimal selectedTotalAmount;
        string currency;
        if (subtotalsByCurrency.Count == 1)
        {
            var single = subtotalsByCurrency.Single();
            selectedTotalAmount = single.Value;
            currency = single.Key;
        }
        else
        {
            selectedTotalAmount = 0m;
            currency = "CNY";
        }

        return new CartDto
        {
            Id = cart.Id,
            UserId = cart.UserId,
            Items = itemDtos,
            SelectedTotalAmount = selectedTotalAmount,
            Currency = currency,
            TotalCount = itemDtos.Sum(i => i.Quantity),
            SubtotalsByCurrency = subtotalsByCurrency
        };
    }

    private static CartItemDto BuildItemDto(CartItem item, Dictionary<Guid, SkuPriceSnapshot> priceMap, bool priceServiceUnavailable = false)
    {
        // 与 CartAppService 对齐：价格服务整体不可用或单 SKU 未命中，标记 PriceUnavailable=true
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