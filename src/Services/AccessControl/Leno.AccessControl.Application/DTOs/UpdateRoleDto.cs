namespace Leno.AccessControl.Application.DTOs;

/// <summary>
/// 更新角色请求 DTO。
/// 由 <see cref="IRoleAppService.UpdateRoleAsync"/> 接收，仅承载可变字段（名称与描述）。
/// </summary>
public sealed class UpdateRoleDto
{
    /// <summary>角色名称（2-64 字符，不可与其他角色重复）。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>角色描述（可选）。</summary>
    public string? Description { get; init; }
}
