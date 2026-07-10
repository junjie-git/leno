using Leno.UserAuth.Domain.ValueObjects;

namespace Leno.UserAuth.Application.DTOs;

/// <summary>
/// 用户资料 DTO，敏感字段（密码哈希）不返回，邮箱与手机号脱敏。
/// </summary>
public sealed class UserDto
{
    public Guid Id { get; init; }

    public string Username { get; init; } = string.Empty;

    public string? Email { get; init; }

    public string? PhoneNumber { get; init; }

    public string Nickname { get; init; } = string.Empty;

    public string? AvatarUrl { get; init; }

    public AccountStatus Status { get; init; }

    public IReadOnlyCollection<string> Roles { get; init; } = Array.Empty<string>();

    public Guid? DefaultAddressId { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime UpdatedAt { get; init; }
}
