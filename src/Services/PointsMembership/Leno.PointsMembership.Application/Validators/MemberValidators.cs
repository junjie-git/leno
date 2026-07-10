using FluentValidation;
using Leno.PointsMembership.Application.DTOs;

namespace Leno.PointsMembership.Application.Validators;

/// <summary>
/// 创建会员等级 DTO 校验。
/// </summary>
public sealed class CreateMembershipLevelDtoValidator : AbstractValidator<CreateMembershipLevelDto>
{
    public CreateMembershipLevelDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Level).GreaterThan(0);
        RuleFor(x => x.MinConsumption).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DiscountRate).InclusiveBetween(0m, 1m);
    }
}

/// <summary>
/// 更新会员等级 DTO 校验。
/// </summary>
public sealed class UpdateMembershipLevelDtoValidator : AbstractValidator<UpdateMembershipLevelDto>
{
    public UpdateMembershipLevelDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.MinConsumption).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DiscountRate).InclusiveBetween(0m, 1m);
    }
}

/// <summary>
/// 创建会员套餐 DTO 校验。
/// </summary>
public sealed class CreateMembershipPackageDtoValidator : AbstractValidator<CreateMembershipPackageDto>
{
    public CreateMembershipPackageDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Level).GreaterThan(0);
        RuleFor(x => x.Price).GreaterThan(0);
        RuleFor(x => x.DurationDays).GreaterThan(0);
        RuleFor(x => x.Benefits).NotEmpty();
    }
}

/// <summary>
/// 更新会员套餐 DTO 校验。
/// </summary>
public sealed class UpdateMembershipPackageDtoValidator : AbstractValidator<UpdateMembershipPackageDto>
{
    public UpdateMembershipPackageDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Price).GreaterThan(0);
        RuleFor(x => x.DurationDays).GreaterThan(0);
        RuleFor(x => x.Benefits).NotEmpty();
    }
}
