using FluentValidation;
using Leno.UserAuth.Application.DTOs;

namespace Leno.UserAuth.Application.Validators;

/// <summary>
/// 修改密码请求校验器。
/// </summary>
public sealed class ChangePasswordDtoValidator : AbstractValidator<ChangePasswordDto>
{
    public ChangePasswordDtoValidator()
    {
        RuleFor(x => x.OldPassword)
            .NotEmpty().WithMessage("旧密码不可为空");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("新密码不可为空")
            .Length(8, 64).WithMessage("密码长度须为 8-64 位")
            .Must(ContainLetterAndDigit).WithMessage("密码须至少包含字母与数字")
            .NotEqual(x => x.OldPassword).WithMessage("新密码不可与旧密码相同");
    }

    private static bool ContainLetterAndDigit(string password)
    {
        var hasLetter = false;
        var hasDigit = false;
        foreach (var c in password)
        {
            if (char.IsLetter(c)) hasLetter = true;
            else if (char.IsDigit(c)) hasDigit = true;
        }

        return hasLetter && hasDigit;
    }
}
