using System.Text.RegularExpressions;
using FluentValidation;
using Leno.UserCenter.Application.DTOs;

namespace Leno.UserCenter.Application.Validators;

/// <summary>
/// 新增/修改地址请求校验器。
/// 从 UserAuth BC 迁入 UserCenter BC（Task A6）。
/// </summary>
public sealed class SaveAddressDtoValidator : AbstractValidator<SaveAddressDto>
{
    private static readonly Regex PhoneRegex = new(@"^\+[1-9]\d{1,14}$", RegexOptions.Compiled);

    public SaveAddressDtoValidator()
    {
        RuleFor(x => x.RecipientName)
            .NotEmpty().WithMessage("收件人不可为空")
            .MaximumLength(32).WithMessage("收件人姓名长度不可超过 32 字符");

        RuleFor(x => x.RecipientPhone)
            .NotEmpty().WithMessage("收件人手机号不可为空")
            .Must(v => PhoneRegex.IsMatch(v)).WithMessage("收件人手机号须为 E.164 格式");

        RuleFor(x => x.Province).NotEmpty().WithMessage("省/直辖市不可为空");
        RuleFor(x => x.City).NotEmpty().WithMessage("市不可为空");
        RuleFor(x => x.District).NotEmpty().WithMessage("区/县不可为空");

        RuleFor(x => x.Detail)
            .NotEmpty().WithMessage("详细地址不可为空")
            .Length(5, 200).WithMessage("详细地址长度须为 5-200 字符");

        RuleFor(x => x.Tag)
            .Must(t => string.IsNullOrWhiteSpace(t) || t.Length <= 8)
            .WithMessage("地址标签长度不可超过 8 字符");
    }
}
