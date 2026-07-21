using FluentValidation;
using Leno.UserAuth.Application.DTOs;
using Leno.UserAuth.Domain.ValueObjects;

namespace Leno.UserAuth.Application.Validators;

/// <summary>
/// 注册请求校验器。用户名/邮箱/手机号正则复用领域共享模式（P2-7）。
/// </summary>
public sealed class RegisterDtoValidator : AbstractValidator<RegisterDto>
{
    public RegisterDtoValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("用户名不可为空")
            .Matches(UsernamePattern.PatternStr).WithMessage(UsernamePattern.ErrorMessage);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("密码不可为空")
            .Length(8, 64).WithMessage("密码长度须为 8-64 位")
            .Must(ContainLetterAndDigit).WithMessage("密码须至少包含字母与数字");

        RuleFor(x => x.Nickname)
            .NotEmpty().WithMessage("昵称不可为空")
            .MaximumLength(32).WithMessage("昵称长度不可超过 32 字符");

        RuleFor(x => x.Email)
            .Must(BeOptionalValidEmail).WithMessage(EmailPattern.ErrorMessage);

        RuleFor(x => x.PhoneNumber)
            .Must(BeOptionalValidPhone).WithMessage(PhonePattern.ErrorMessage);

        RuleFor(x => x.AvatarUrl)
            .Must(BeOptionalHttpsUrl).WithMessage("头像 URL 必须为 HTTPS");

        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.Email) || !string.IsNullOrWhiteSpace(x.PhoneNumber))
            .WithMessage("必须提供邮箱或手机号之一");
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

    private static bool BeOptionalValidEmail(string? email)
        => string.IsNullOrWhiteSpace(email) || EmailPattern.GetRegex().IsMatch(email);

    private static bool BeOptionalValidPhone(string? phone)
        => string.IsNullOrWhiteSpace(phone) || PhonePattern.GetRegex().IsMatch(phone);

    private static bool BeOptionalHttpsUrl(string? url)
        => string.IsNullOrWhiteSpace(url)
            || (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps);
}
