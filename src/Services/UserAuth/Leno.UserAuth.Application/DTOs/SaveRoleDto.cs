namespace Leno.UserAuth.Application.DTOs;

/// <summary>
/// 保存角色请求 DTO。
/// </summary>
public sealed class SaveRoleDto
{
    /// <summary>角色名称。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>角色描述。</summary>
    public string? Description { get; init; }
}