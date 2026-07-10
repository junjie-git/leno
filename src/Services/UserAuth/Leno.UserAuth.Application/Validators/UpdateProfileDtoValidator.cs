using FluentValidation;
using Leno.UserAuth.Application.DTOs;

namespace Leno.UserAuth.Application.Validators;

/// <summary>
/// 修改个人资料请求校验器。
/// </summary>
public sealed class UpdateProfileDtoValidator : AbstractValidator<UpdateProfileDto>
{
    public UpdateProfileDtoValidator()
    {
        RuleFor(x => x.Nickname)
            .NotEmpty().WithMessage("昵称不可为空")
            .MaximumLength(32).WithMessage("昵称长度不可超过 32 字符");

        RuleFor(x => x.AvatarUrl)
            .Must(BeOptionalHttpsUrl).WithMessage("头像 URL 必须为 HTTPS");
    }

    private static bool BeOptionalHttpsUrl(string? url)
        => string.IsNullOrWhiteSpace(url)
            || (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps);
}
