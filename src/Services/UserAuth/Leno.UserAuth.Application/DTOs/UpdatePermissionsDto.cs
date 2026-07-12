namespace Leno.UserAuth.Application.DTOs;

/// <summary>
/// 更新角色权限请求 DTO。
/// </summary>
public sealed class UpdatePermissionsDto
{
    /// <summary>权限资源键列表，全量替换。</summary>
    public List<string> Permissions { get; init; } = new();
}