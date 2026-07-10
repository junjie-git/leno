using FluentValidation;
using Leno.Product.Application.DTOs;

namespace Leno.Product.Application.Validators;

/// <summary>
/// 更新分类 DTO 校验器。
/// </summary>
public sealed class UpdateCategoryDtoValidator : AbstractValidator<UpdateCategoryDto>
{
    public UpdateCategoryDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("分类名称不可为空")
            .MaximumLength(50).WithMessage("分类名称长度不可超过 50 字符");

        RuleFor(x => x.SortOrder)
            .InclusiveBetween(0, 9999).WithMessage("排序值须为 0-9999");
    }
}
