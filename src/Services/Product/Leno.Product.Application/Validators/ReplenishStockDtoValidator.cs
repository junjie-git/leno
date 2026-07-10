using FluentValidation;
using Leno.Product.Application.DTOs;

namespace Leno.Product.Application.Validators;

/// <summary>
/// 库存补货 DTO 校验器。
/// </summary>
public sealed class ReplenishStockDtoValidator : AbstractValidator<ReplenishStockDto>
{
    public ReplenishStockDtoValidator()
    {
        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("补货数量须大于 0");
    }
}
