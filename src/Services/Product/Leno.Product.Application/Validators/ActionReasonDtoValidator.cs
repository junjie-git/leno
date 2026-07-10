using FluentValidation;
using Leno.Product.Application.DTOs;

namespace Leno.Product.Application.Validators;

/// <summary>
/// 带原因的操作 DTO 校验器，用于商品驳回、下架等操作。
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
