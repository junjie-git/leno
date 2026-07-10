using FluentValidation;
using Leno.UserAuth.Application.DTOs;

namespace Leno.UserAuth.Application.Validators;

/// <summary>
/// 登录请求校验器。
/// </summary>
public sealed class LoginDtoValidator : AbstractValidator<LoginDto>
{
    public LoginDtoValidator()
    {
        RuleFor(x => x.Account)
            .NotEmpty().WithMessage("账号不可为空");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("密码不可为空");
    }
}
