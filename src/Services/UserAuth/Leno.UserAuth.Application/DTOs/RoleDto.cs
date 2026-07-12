namespace Leno.UserAuth.Application.DTOs;

/// <summary>
/// 角色 DTO。
/// </summary>
public sealed class RoleDto
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public bool IsBuiltIn { get; init; }

    public IReadOnlyCollection<string> Permissions { get; init; } = Array.Empty<string>();

    public DateTime CreatedAt { get; init; }

    public DateTime UpdatedAt { get; init; }
}