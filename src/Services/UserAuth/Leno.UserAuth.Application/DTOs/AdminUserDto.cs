using Leno.UserAuth.Domain.ValueObjects;

namespace Leno.UserAuth.Application.DTOs;

/// <summary>
/// 管理后台用户列表项 DTO。
/// </summary>
public sealed class AdminUserDto
{
    public Guid Id { get; init; }

    public string Username { get; init; } = string.Empty;

    public string? Email { get; init; }

    public string? PhoneNumber { get; init; }

    public string Nickname { get; init; } = string.Empty;

    public AccountStatus Status { get; init; }

    public IReadOnlyCollection<string> Roles { get; init; } = Array.Empty<string>();

    public int FailedLoginCount { get; init; }

    public DateTime? LockedUntil { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime UpdatedAt { get; init; }
}
