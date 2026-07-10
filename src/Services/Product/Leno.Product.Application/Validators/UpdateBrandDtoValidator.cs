using FluentValidation;
using Leno.Product.Application.DTOs;

namespace Leno.Product.Application.Validators;

/// <summary>
/// 更新品牌 DTO 校验器。
/// </summary>
public sealed class UpdateBrandDtoValidator : AbstractValidator<UpdateBrandDto>
{
    public UpdateBrandDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("品牌名称不可为空")
            .MaximumLength(50).WithMessage("品牌名称长度不可超过 50 字符");

        RuleFor(x => x.Logo)
            .MaximumLength(512).WithMessage("Logo URL 长度不可超过 512 字符");
    }
}
