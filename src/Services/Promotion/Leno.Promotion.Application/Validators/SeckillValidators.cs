using FluentValidation;
using Leno.Promotion.Application.DTOs;

namespace Leno.Promotion.Application.Validators;

/// <summary>
/// 创建秒杀活动 DTO 校验。
/// </summary>
public sealed class CreateSeckillActivityDtoValidator : AbstractValidator<CreateSeckillActivityDto>
{
    public CreateSeckillActivityDtoValidator()
    {
        RuleFor(x => x.SpuId).NotEqual(Guid.Empty).WithMessage("SpuId 不可为空");
        RuleFor(x => x.SkuId).NotEqual(Guid.Empty).WithMessage("SkuId 不可为空");
        RuleFor(x => x.SeckillPrice).GreaterThan(0).LessThan(x => x.OriginalPrice)
            .WithMessage("秒杀价须大于 0 且小于原价");
        RuleFor(x => x.OriginalPrice).GreaterThan(0);
        RuleFor(x => x.TotalStock).GreaterThan(0);
        RuleFor(x => x.LimitPerUser).GreaterThan(0);
        RuleFor(x => x.EndTime).GreaterThan(x => x.StartTime);
    }
}

/// <summary>
/// 秒杀下单 DTO 校验。
/// </summary>
public sealed class SeckillPlaceOrderDtoValidator : AbstractValidator<SeckillPlaceOrderDto>
{
    public SeckillPlaceOrderDtoValidator()
    {
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("下单数量须大于 0");
    }
}
