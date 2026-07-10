using FluentValidation;
using Leno.SellerShop.Application.DTOs;

namespace Leno.SellerShop.Application.Validators;

/// <summary>
/// 卖家入驻申请校验器。
/// </summary>
public sealed class SubmitShopApplicationDtoValidator : AbstractValidator<SubmitShopApplicationDto>
{
    public SubmitShopApplicationDtoValidator()
    {
        RuleFor(x => x.ShopName)
            .NotEmpty().WithMessage("店铺名称不可为空")
            .Length(2, 32).WithMessage("店铺名称长度须为 2-32 字符");

        RuleFor(x => x.ContactPhone)
            .NotEmpty().WithMessage("客服电话不可为空")
            .MaximumLength(20).WithMessage("客服电话长度不可超过 20 字符");

        RuleFor(x => x.ContactEmail)
            .MaximumLength(256).WithMessage("客服邮箱长度不可超过 256 字符")
            .Must(BeOptionalValidEmail).WithMessage("客服邮箱格式不正确");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("店铺描述长度不可超过 1000 字符");

        RuleFor(x => x.Logo)
            .MaximumLength(512).WithMessage("Logo URL 长度不可超过 512 字符");

        RuleFor(x => x.Address)
            .MaximumLength(256).WithMessage("经营地址长度不可超过 256 字符");

        RuleFor(x => x.BusinessLicenseNo)
            .MaximumLength(32).WithMessage("营业执照号长度不可超过 32 字符");

        RuleFor(x => x.RealName)
            .NotEmpty().WithMessage("真实姓名不可为空")
            .MaximumLength(32).WithMessage("真实姓名长度不可超过 32 字符");

        RuleFor(x => x.IdCard)
            .MaximumLength(18).WithMessage("身份证号长度不可超过 18 字符");

        RuleFor(x => x.BankAccount)
            .MaximumLength(64).WithMessage("收款银行账号长度不可超过 64 字符");

        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.IdCard) || !string.IsNullOrWhiteSpace(x.BusinessLicenseNo))
            .WithMessage("须提供身份证号或营业执照号之一");
    }

    private static bool BeOptionalValidEmail(string? email)
        => string.IsNullOrWhiteSpace(email)
            || (email.Contains('@') && !email.StartsWith('@') && !email.EndsWith('@'));
}
