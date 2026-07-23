using Leno.AccessControl.Domain.ValueObjects;

namespace Leno.AccessControl.Application.Services;

/// <summary>
/// 用户角色分配应用服务接口。
/// 从 UserAuth BC 的 UserAdminAppService 角色分配职责拆出（3.6 AuthN/AuthZ 拆分）。
/// </summary>
public interface IUserRoleAppService
{
    /// <summary>查询用户当前生效的角色编码列表。</summary>
    Task<List<string>> GetUserRolesAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// 分配角色（幂等：已存在则忽略）。
    /// </summary>
    Task AssignRoleAsync(Guid userId, RoleType role, Guid? operatorId = null, CancellationToken ct = default);

    /// <summary>
    /// 撤销角色。禁止移除最后一个角色（INV-12），禁止管理员撤销自身 Admin 角色（INV-13）。
    /// </summary>
    Task RevokeRoleAsync(Guid userId, RoleType role, Guid? operatorId = null, CancellationToken ct = default);

    /// <summary>注册时分配初始 Buyer 角色（自助注册场景）。</summary>
    Task AssignDefaultRoleOnRegisterAsync(Guid userId, CancellationToken ct = default);
}
