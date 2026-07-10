using FluentValidation;
using Leno.Product.Application.DTOs;

namespace Leno.Product.Application.Validators;

/// <summary>
/// 新增 SKU DTO 校验器。
/// </summary>
public sealed class AddSkuDtoValidator : AbstractValidator<AddSkuDto>
{
    public AddSkuDtoValidator()
    {
        RuleFor(x => x.SkuCode)
            .NotEmpty().WithMessage("SKU 编码不可为空")
            .MaximumLength(64).WithMessage("SKU 编码长度不可超过 64 字符");

        RuleFor(x => x.Price)
            .GreaterThan(0m).WithMessage("SKU 价格须大于 0");

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("币种不可为空")
            .Length(3).WithMessage("币种须为 3 位 ISO 4217 代码");

        RuleFor(x => x.StockQty)
            .GreaterThanOrEqualTo(0).WithMessage("SKU 库存不可为负");

        RuleFor(x => x.SpecAttributes)
            .Must(attrs => attrs is not null && attrs.Any(a => !string.IsNullOrWhiteSpace(a.Name) && !string.IsNullOrWhiteSpace(a.Value)))
            .WithMessage("SKU 至少需要 1 项规格属性");

        RuleForEach(x => x.SpecAttributes)
            .ChildRules(attr =>
            {
                attr.RuleFor(a => a.Name)
                    .NotEmpty().WithMessage("规格名不可为空")
                    .MaximumLength(50).WithMessage("规格名长度不可超过 50 字符");
                attr.RuleFor(a => a.Value)
                    .NotEmpty().WithMessage("规格值不可为空")
                    .MaximumLength(50).WithMessage("规格值长度不可超过 50 字符");
            });

        RuleFor(x => x.ImageUrl)
            .MaximumLength(512).WithMessage("SKU 图片 URL 长度不可超过 512 字符");
    }
}
