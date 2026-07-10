using FluentValidation;
using Leno.Promotion.Application.DTOs;

namespace Leno.Promotion.Application.Validators;

/// <summary>
/// 创建满减活动 DTO 校验。
/// </summary>
public sealed class CreatePromotionActivityDtoValidator : AbstractValidator<CreatePromotionActivityDto>
{
    public CreatePromotionActivityDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.EndTime).GreaterThan(x => x.StartTime);
        RuleFor(x => x.Rules).NotEmpty().WithMessage("至少需要一条满减规则");
        RuleForEach(x => x.Rules).ChildRules(rule =>
        {
            rule.RuleFor(r => r.ThresholdAmount).GreaterThanOrEqualTo(0);
            rule.RuleFor(r => r.DiscountAmount).GreaterThan(0).LessThanOrEqualTo(r => r.ThresholdAmount);
        });
    }
}

/// <summary>
/// 更新满减活动 DTO 校验。
/// </summary>
public sealed class UpdatePromotionActivityDtoValidator : AbstractValidator<UpdatePromotionActivityDto>
{
    public UpdatePromotionActivityDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
    }
}

/// <summary>
/// 创建优惠券模板 DTO 校验。
/// </summary>
public sealed class CreateCouponDtoValidator : AbstractValidator<CreateCouponDto>
{
    public CreateCouponDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.FaceValue).GreaterThan(0);
        RuleFor(x => x.MinSpend).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TotalQty).NotEqual(0).WithMessage("发放总量须为正数或 -1（不限量）");
    }
}

/// <summary>
/// 更新优惠券模板 DTO 校验。
/// </summary>
public sealed class UpdateCouponDtoValidator : AbstractValidator<UpdateCouponDto>
{
    public UpdateCouponDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.FaceValue).GreaterThan(0);
        RuleFor(x => x.MinSpend).GreaterThanOrEqualTo(0);
    }
}
