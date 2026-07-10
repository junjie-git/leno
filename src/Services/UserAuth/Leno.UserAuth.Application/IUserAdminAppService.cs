using Leno.UserAuth.Application.DTOs;

namespace Leno.UserAuth.Application;

/// <summary>
/// 用户管理后台应用服务，编排用户分页查询、角色分配与账户状态管理用例。
/// 仅 Operator/Admin 可访问，写操作经审计中间件在事务内写入审计日志。
/// </summary>
public interface IUserAdminAppService
{
    /// <summary>分页查询用户列表。</summary>
    Task<PagedResult<AdminUserDto>> QueryUsersAsync(AdminUserQueryDto query, CancellationToken ct = default);

    /// <summary>查询用户详情。</summary>
    Task<AdminUserDto> GetUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>为用户分配角色（幂等，附加角色不会移除已有角色）。</summary>
    Task AssignRolesAsync(Guid targetUserId, AssignRolesDto dto, Guid operatorId, CancellationToken ct = default);

    /// <summary>锁定用户账户。</summary>
    Task SuspendAsync(Guid targetUserId, SuspendUserDto dto, Guid operatorId, CancellationToken ct = default);

    /// <summary>解锁或恢复用户账户为 Active。</summary>
    Task ResumeAsync(Guid targetUserId, Guid operatorId, CancellationToken ct = default);
}
