namespace Leno.UserAuth.Application.DTOs;

/// <summary>
/// 管理员分配角色请求 DTO。
/// </summary>
public sealed class AssignRolesDto
{
    /// <summary>待分配的角色编码集合（Buyer/Seller/Operator/Admin）。</summary>
    public IReadOnlyCollection<string> Roles { get; init; } = Array.Empty<string>();
}
