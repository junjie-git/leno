using Leno.Cart.Application.DTOs;
using Leno.SharedKernel.ValueObjects;

namespace Leno.Cart.Application;

/// <summary>
/// 购物车管理应用服务，编排添加/修改/删除/选中/查询/结算预览/合并用例。
/// </summary>
public interface ICartAppService
{
    /// <summary>添加购物车项（加载购物车→校验 SKU→添加→保存）。</summary>
    Task<CartDto> AddItemAsync(Guid userId, AddCartItemDto dto, CancellationToken ct = default);

    /// <summary>更新购物车项数量。</summary>
    Task<CartDto> UpdateQuantityAsync(Guid userId, Guid skuId, UpdateCartItemQuantityDto dto, CancellationToken ct = default);

    /// <summary>移除购物车项。</summary>
    Task<CartDto> RemoveItemAsync(Guid userId, Guid skuId, CancellationToken ct = default);

    /// <summary>批量选中/取消选中。</summary>
    Task<CartDto> SelectItemsAsync(Guid userId, SelectCartItemsDto dto, CancellationToken ct = default);

    /// <summary>全选/取消全选所有有效项。无效项不受影响。</summary>
    Task<CartDto> ToggleAllSelectionAsync(Guid userId, bool isSelected, CancellationToken ct = default);

    /// <summary>获取购物车（附加实时价格与可售状态）。</summary>
    Task<CartDto> GetCartAsync(Guid userId, CancellationToken ct = default);

    /// <summary>结算预览（按卖家分组返回选中项）。</summary>
    Task<CheckoutPreviewDto> PreviewCheckoutAsync(Guid userId, CancellationToken ct = default);

    /// <summary>登录时合并匿名购物车：遍历匿名购物车项逐项合并，合并后删除匿名购物车。</summary>
    Task<CartDto> MergeAnonymousCartAsync(Guid userId, string anonymousId, CancellationToken ct = default);
}
