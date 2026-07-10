using FluentValidation;
using Leno.SellerShop.Application.DTOs;

namespace Leno.SellerShop.Application.Validators;

/// <summary>
/// 店铺信息更新校验器。
/// </summary>
public sealed class UpdateShopInfoDtoValidator : AbstractValidator<UpdateShopInfoDto>
{
    public UpdateShopInfoDtoValidator()
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
    }

    private static bool BeOptionalValidEmail(string? email)
        => string.IsNullOrWhiteSpace(email)
            || (email.Contains('@') && !email.StartsWith('@') && !email.EndsWith('@'));
}
