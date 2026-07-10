using FluentValidation;
using Leno.Cart.Application.DTOs;

namespace Leno.Cart.Application.Validators;

/// <summary>
/// 添加购物车项 DTO 校验。
/// </summary>
public sealed class AddCartItemDtoValidator : AbstractValidator<AddCartItemDto>
{
    public AddCartItemDtoValidator()
    {
        RuleFor(x => x.SkuId).NotEqual(Guid.Empty).WithMessage("SkuId 不可为空");
        RuleFor(x => x.SellerId).NotEqual(Guid.Empty).WithMessage("SellerId 不可为空");
        RuleFor(x => x.Quantity).InclusiveBetween(1, 99).WithMessage("购买数量须在 1-99 之间");
    }
}

/// <summary>
/// 更新购物车项数量 DTO 校验。
/// </summary>
public sealed class UpdateCartItemQuantityDtoValidator : AbstractValidator<UpdateCartItemQuantityDto>
{
    public UpdateCartItemQuantityDtoValidator()
    {
        RuleFor(x => x.Quantity).InclusiveBetween(1, 99).WithMessage("购买数量须在 1-99 之间");
    }
}

/// <summary>
/// 批量选中购物车项 DTO 校验。
/// </summary>
public sealed class SelectCartItemsDtoValidator : AbstractValidator<SelectCartItemsDto>
{
    public SelectCartItemsDtoValidator()
    {
        RuleFor(x => x.SkuIds).NotEmpty().WithMessage("SkuIds 不可为空");
    }
}
