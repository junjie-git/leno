using FluentValidation;
using Leno.PointsMembership.Application.DTOs;

namespace Leno.PointsMembership.Application.Validators;

/// <summary>
/// 手动发放积分 DTO 校验。
/// </summary>
public sealed class AwardPointsDtoValidator : AbstractValidator<AwardPointsDto>
{
    public AwardPointsDtoValidator()
    {
        RuleFor(x => x.UserId).NotEqual(Guid.Empty).WithMessage("UserId 不可为空");
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("发放积分数量须大于 0");
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(256);
    }
}
