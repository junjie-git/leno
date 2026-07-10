using System.Text.RegularExpressions;
using FluentValidation;
using Leno.UserAuth.Application.DTOs;

namespace Leno.UserAuth.Application.Validators;

/// <summary>
/// 注册请求校验器。
/// </summary>
public sealed class RegisterDtoValidator : AbstractValidator<RegisterDto>
{
    private static readonly Regex UsernameRegex = new("^[a-zA-Z0-9_]{3,32}$", RegexOptions.Compiled);
    private static readonly Regex PhoneRegex = new(@"^\+[1-9]\d{1,14}$", RegexOptions.Compiled);
    private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public RegisterDtoValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("用户名不可为空")
            .Must(v => UsernameRegex.IsMatch(v)).WithMessage("用户名仅允许字母、数字与下划线，长度 3-32");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("密码不可为空")
            .Length(8, 64).WithMessage("密码长度须为 8-64 位")
            .Must(ContainLetterAndDigit).WithMessage("密码须至少包含字母与数字");

        RuleFor(x => x.Nickname)
            .NotEmpty().WithMessage("昵称不可为空")
            .MaximumLength(32).WithMessage("昵称长度不可超过 32 字符");

        RuleFor(x => x.Email)
            .Must(BeOptionalValidEmail).WithMessage("邮箱格式不正确");

        RuleFor(x => x.PhoneNumber)
            .Must(BeOptionalValidPhone).WithMessage("手机号须为 E.164 格式");

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
        => string.IsNullOrWhiteSpace(email) || EmailRegex.IsMatch(email);

    private static bool BeOptionalValidPhone(string? phone)
        => string.IsNullOrWhiteSpace(phone) || PhoneRegex.IsMatch(phone);

    private static bool BeOptionalHttpsUrl(string? url)
        => string.IsNullOrWhiteSpace(url)
            || (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps);
}
