using Leno.Identity.Application.DTOs;

namespace Leno.Identity.Application;

/// <summary>
/// 用户管理后台应用服务接口（Identity BC，Task A2 补齐）。
/// 承载用户分页查询、详情查询、角色分配与账户状态管理用例，供 A4 AdminUsersController 消费。
/// <para>
/// 角色变更（<see cref="AssignRolesAsync"/>）通过 HTTP 调 AccessControl BC 端点（Spec §4.3.2 推荐方案），
/// Identity BC 本身不持久化角色数据。
/// </para>
/// </summary>
public interface IUserAdminAppService
{
    /// <summary>分页查询用户列表。</summary>
    /// <param name="query">查询参数（关键词、状态过滤）。</param>
    /// <param name="page">页码，从 1 起。</param>
    /// <param name="pageSize">每页大小，最大 100。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>分页结果。</returns>
    Task<PagedResult<AdminUserDto>> QueryUsersAsync(AdminUserQueryDto query, int page, int pageSize, CancellationToken ct = default);

    /// <summary>查询用户详情。</summary>
    /// <param name="userId">用户标识。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>用户详情 DTO。</returns>
    Task<AdminUserDto> GetUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// 为用户分配角色（跨域调用 AccessControl BC）。
    /// 通过 HTTP POST 调 AccessControl <c>api/admin/users/{userId}/roles</c> 端点，
    /// 成功后撤销该用户所有刷新令牌，使角色变更立即生效。
    /// </summary>
    /// <param name="userId">目标用户标识。</param>
    /// <param name="roleIds">角色标识列表。</param>
    /// <param name="ct">取消令牌。</param>
    Task AssignRolesAsync(Guid userId, List<Guid> roleIds, CancellationToken ct = default);

    /// <summary>锁定用户账户。</summary>
    /// <param name="userId">目标用户标识。</param>
    /// <param name="request">锁定请求（含原因与时长）。</param>
    /// <param name="ct">取消令牌。</param>
    Task SuspendAsync(Guid userId, SuspendUserDto request, CancellationToken ct = default);

    /// <summary>解锁或恢复用户账户为 Active。</summary>
    /// <param name="userId">目标用户标识。</param>
    /// <param name="ct">取消令牌。</param>
    Task ResumeAsync(Guid userId, CancellationToken ct = default);
}
