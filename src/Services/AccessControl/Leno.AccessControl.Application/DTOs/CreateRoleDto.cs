namespace Leno.AccessControl.Application.DTOs;

/// <summary>
/// 创建角色请求 DTO。
/// 由 <see cref="IRoleAppService.CreateRoleAsync"/> 接收，与 <see cref="UpdateRoleDto"/> 区分
/// （未来扩展时可加入初始权限集合等创建专用字段，目前保持最小集）。
/// </summary>
public sealed class CreateRoleDto
{
    /// <summary>角色名称（2-64 字符，不可重复）。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>角色描述（可选）。</summary>
    public string? Description { get; init; }
}
