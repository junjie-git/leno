using FluentValidation;
using Leno.SellerShop.Application.DTOs;

namespace Leno.SellerShop.Application.Validators;

/// <summary>
/// 卖家档案提交校验器。
/// </summary>
public sealed class SubmitSellerProfileDtoValidator : AbstractValidator<SubmitSellerProfileDto>
{
    public SubmitSellerProfileDtoValidator()
    {
        RuleFor(x => x.RealName)
            .NotEmpty().WithMessage("真实姓名不可为空")
            .MaximumLength(32).WithMessage("真实姓名长度不可超过 32 字符");

        RuleFor(x => x.IdCard)
            .MaximumLength(18).WithMessage("身份证号长度不可超过 18 字符");

        RuleFor(x => x.BusinessLicenseNo)
            .MaximumLength(32).WithMessage("营业执照号长度不可超过 32 字符");

        RuleFor(x => x.BankAccount)
            .MaximumLength(64).WithMessage("收款银行账号长度不可超过 64 字符");

        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.IdCard) || !string.IsNullOrWhiteSpace(x.BusinessLicenseNo))
            .WithMessage("须提供身份证号或营业执照号之一");
    }
}
