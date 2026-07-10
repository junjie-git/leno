using FluentValidation;
using Leno.Product.Application.DTOs;

namespace Leno.Product.Application.Validators;

/// <summary>
/// 创建商品（草稿）DTO 校验器。
/// </summary>
public sealed class CreateProductDtoValidator : AbstractValidator<CreateProductDto>
{
    public CreateProductDtoValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("商品标题不可为空")
            .Length(2, 100).WithMessage("商品标题长度须为 2-100 字符");

        RuleFor(x => x.Subtitle)
            .MaximumLength(200).WithMessage("副标题长度不可超过 200 字符");

        RuleFor(x => x.MainImageUrl)
            .NotEmpty().WithMessage("主图 URL 不可为空")
            .MaximumLength(512).WithMessage("主图 URL 长度不可超过 512 字符");

        RuleFor(x => x.CategoryId)
            .NotEqual(Guid.Empty).WithMessage("分类标识不可为空");

        RuleFor(x => x.Specs)
            .Must(specs => specs is null || specs.Count(s => !string.IsNullOrWhiteSpace(s)) <= 10)
            .WithMessage("规格维度数量不可超过 10");

        RuleForEach(x => x.Images)
            .ChildRules(image =>
            {
                image.RuleFor(i => i.Url)
                    .NotEmpty().WithMessage("图片 URL 不可为空")
                    .MaximumLength(512).WithMessage("图片 URL 长度不可超过 512 字符");
            });
    }
}
