using FluentValidation;
using Leno.Order.Application.DTOs;

namespace Leno.Order.Application.Validators;

/// <summary>
/// 创建订单 DTO 校验。
/// </summary>
public sealed class CreateOrderDtoValidator : AbstractValidator<CreateOrderDto>
{
    public CreateOrderDtoValidator()
    {
        RuleFor(x => x.Items).NotEmpty();
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.SkuId).NotEqual(Guid.Empty);
            item.RuleFor(i => i.Quantity).GreaterThan(0);
        });
        RuleFor(x => x.PointsToUse).GreaterThanOrEqualTo(0);
        RuleFor(x => x.RecipientName).NotEmpty().MaximumLength(64);
        RuleFor(x => x.RecipientPhone).NotEmpty().MaximumLength(32);
        RuleFor(x => x.Province).NotEmpty();
        RuleFor(x => x.City).NotEmpty();
        RuleFor(x => x.District).NotEmpty();
        RuleFor(x => x.Detail).NotEmpty();
    }
}

/// <summary>
/// 立即购买 DTO 校验。
/// </summary>
public sealed class BuyNowDtoValidator : AbstractValidator<BuyNowDto>
{
    public BuyNowDtoValidator()
    {
        RuleFor(x => x.SkuId).NotEqual(Guid.Empty);
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.PointsToUse).GreaterThanOrEqualTo(0);
        RuleFor(x => x.RecipientName).NotEmpty().MaximumLength(64);
        RuleFor(x => x.RecipientPhone).NotEmpty().MaximumLength(32);
        RuleFor(x => x.Province).NotEmpty();
        RuleFor(x => x.City).NotEmpty();
        RuleFor(x => x.District).NotEmpty();
        RuleFor(x => x.Detail).NotEmpty();
    }
}

/// <summary>
/// 发货 DTO 校验。
/// </summary>
public sealed class ShipOrderDtoValidator : AbstractValidator<ShipOrderDto>
{
    public ShipOrderDtoValidator()
    {
        RuleFor(x => x.LogisticsNo).NotEmpty().MaximumLength(64);
    }
}

/// <summary>
/// 买家取消订单 DTO 校验。
/// </summary>
public sealed class CancelOrderDtoValidator : AbstractValidator<CancelOrderDto>
{
    public CancelOrderDtoValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(256);
    }
}

/// <summary>
/// 创建物流公司 DTO 校验。
/// </summary>
public sealed class CreateLogisticsCompanyDtoValidator : AbstractValidator<CreateLogisticsCompanyDto>
{
    public CreateLogisticsCompanyDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(64);
    }
}

/// <summary>
/// 创建运费模板 DTO 校验。
/// </summary>
public sealed class CreateFreightTemplateDtoValidator : AbstractValidator<CreateFreightTemplateDto>
{
    public CreateFreightTemplateDtoValidator()
    {
        RuleFor(x => x.SellerId).NotEqual(Guid.Empty);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleForEach(x => x.RegionRules).ChildRules(rule =>
        {
            rule.RuleFor(r => r.RegionCode).NotEmpty();
            rule.RuleFor(r => r.FirstUnit).GreaterThan(0);
            rule.RuleFor(r => r.FirstPrice).GreaterThanOrEqualTo(0);
            rule.RuleFor(r => r.AdditionalUnit).GreaterThan(0);
            rule.RuleFor(r => r.AdditionalPrice).GreaterThanOrEqualTo(0);
        });
    }
}
