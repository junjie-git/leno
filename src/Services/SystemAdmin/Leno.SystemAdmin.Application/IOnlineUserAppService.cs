using Leno.SystemAdmin.Application.DTOs;
using Leno.Infrastructure.Abstractions.Sessions;

namespace Leno.SystemAdmin.Application;

/// <summary>
/// 在线用户管理应用服务接口。
/// 数据源为 Redis 会话存储（IUserSessionStore），不进入 EF Core。
/// Redis 不可用时降级为空列表（不阻塞页面渲染）。
/// </summary>
public interface IOnlineUserAppService
{
    /// <summary>分页查询在线用户，派生 SessionDurationMs 与 IsAnomaly。</summary>
    Task<OnlineUserListResultDto> QueryAsync(OnlineUserQuery query, CancellationToken ct = default);

    /// <summary>按 sessionId 获取在线用户详情，不存在返回 null。</summary>
    Task<OnlineUserDto?> GetByIdAsync(string sessionId, CancellationToken ct = default);

    /// <summary>获取在线用户统计指标。</summary>
    Task<OnlineUserStatsDto> GetStatsAsync(CancellationToken ct = default);

    /// <summary>
    /// 强制下线指定会话。sessionId == 当前操作者 sessionId 时抛
    /// SystemAdminDomainException(code ONLINE_USER_FORCE_OFFLINE_SELF_FORBIDDEN)。
    /// </summary>
    Task ForceOfflineAsync(string sessionId, string currentOperatorSessionId, CancellationToken ct = default);
}
