using Leno.Cart.Application.DTOs;

namespace Leno.Cart.Application;

/// <summary>
/// 匿名购物车应用服务，编排添加/修改/删除/选中/查询/结算预览用例。
/// 以会话标识（sessionId）为键，无需用户认证。
/// </summary>
public interface IAnonymousCartAppService
{
    /// <summary>创建匿名购物车并返回会话标识与购物车。</summary>
    Task<AnonymousCartResponseDto> CreateCartAsync(CancellationToken ct = default);

    /// <summary>获取匿名购物车（含实时价格与可售状态）。</summary>
    Task<CartDto> GetCartAsync(string sessionId, CancellationToken ct = default);

    /// <summary>添加购物车项。</summary>
    Task<CartDto> AddItemAsync(string sessionId, AddCartItemDto dto, CancellationToken ct = default);

    /// <summary>更新购物车项数量。</summary>
    Task<CartDto> UpdateQuantityAsync(string sessionId, Guid skuId, UpdateCartItemQuantityDto dto, CancellationToken ct = default);

    /// <summary>移除购物车项。</summary>
    Task<CartDto> RemoveItemAsync(string sessionId, Guid skuId, CancellationToken ct = default);

    /// <summary>批量选中/取消选中。</summary>
    Task<CartDto> SelectItemsAsync(string sessionId, SelectCartItemsDto dto, CancellationToken ct = default);

    /// <summary>结算预览（按卖家分组返回选中项）。</summary>
    Task<CheckoutPreviewDto> PreviewCheckoutAsync(string sessionId, CancellationToken ct = default);
}