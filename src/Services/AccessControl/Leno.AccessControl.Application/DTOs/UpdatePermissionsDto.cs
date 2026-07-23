namespace Leno.AccessControl.Application.DTOs;

/// <summary>
/// 更新角色权限请求 DTO。
/// 从 UserAuth BC 迁入 AccessControl BC（3.6 AuthN/AuthZ 拆分）。
/// </summary>
public sealed class UpdatePermissionsDto
{
    /// <summary>权限资源键列表，全量替换。</summary>
    public List<string> Permissions { get; init; } = new();
}
