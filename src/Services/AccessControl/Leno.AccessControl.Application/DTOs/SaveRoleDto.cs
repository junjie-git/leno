namespace Leno.AccessControl.Application.DTOs;

/// <summary>
/// 保存角色请求 DTO。
/// 从 UserAuth BC 迁入 AccessControl BC（3.6 AuthN/AuthZ 拆分）。
/// </summary>
public sealed class SaveRoleDto
{
    /// <summary>角色名称。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>角色描述。</summary>
    public string? Description { get; init; }
}
