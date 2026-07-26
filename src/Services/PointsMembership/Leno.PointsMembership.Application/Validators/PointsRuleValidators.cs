using FluentValidation;
using Leno.PointsMembership.Application.DTOs;
using Leno.PointsMembership.Domain.Aggregates;

namespace Leno.PointsMembership.Application.Validators;

/// <summary>
/// 创建积分规则 DTO 校验。
/// 对应 design-prompts points-rules.md §4 校验规则：
/// - 编码必填唯一
/// - 积分值须为整数，-1000 到 1000 之间（支持扣减）
/// - 每日上限须为正整数，1-100 之间
/// </summary>
public sealed class CreatePointsRuleDtoValidator : AbstractValidator<CreatePointsRuleDto>
{
    public CreatePointsRuleDtoValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .MaximumLength(64)
            .Matches("^[A-Z][A-Z0-9_]*$")
            .WithMessage("规则编码须为大写字母开头，仅含大写字母、数字与下划线");

        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);

        RuleFor(x => x.Points)
            .InclusiveBetween(PointsRule.PointsMinValue, PointsRule.PointsMaxValue);

        RuleFor(x => x.DailyLimit)
            .InclusiveBetween(PointsRule.DailyLimitMinValue, PointsRule.DailyLimitMaxValue);
    }
}

/// <summary>
/// 更新积分规则 DTO 校验（编码不可改，状态经启用/停用端点切换）。
/// </summary>
public sealed class UpdatePointsRuleDtoValidator : AbstractValidator<UpdatePointsRuleDto>
{
    public UpdatePointsRuleDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);

        RuleFor(x => x.Points)
            .InclusiveBetween(PointsRule.PointsMinValue, PointsRule.PointsMaxValue);

        RuleFor(x => x.DailyLimit)
            .InclusiveBetween(PointsRule.DailyLimitMinValue, PointsRule.DailyLimitMaxValue);
    }
}
