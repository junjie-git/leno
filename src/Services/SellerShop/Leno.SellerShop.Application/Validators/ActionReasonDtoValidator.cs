using FluentValidation;
using Leno.SellerShop.Application.DTOs;

namespace Leno.SellerShop.Application.Validators;

/// <summary>
/// 带原因的操作 DTO 校验器，用于驳回、暂停、关闭等操作。
/// </summary>
public sealed class ActionReasonDtoValidator : AbstractValidator<ActionReasonDto>
{
    public ActionReasonDtoValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("操作原因不可为空")
            .MaximumLength(200).WithMessage("操作原因长度不可超过 200 字符");
    }
}
