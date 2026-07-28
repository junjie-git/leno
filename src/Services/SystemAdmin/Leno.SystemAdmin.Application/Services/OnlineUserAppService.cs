using Leno.Infrastructure.Abstractions.Sessions;
using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Leno.SystemAdmin.Application.Services;

/// <summary>
/// 在线用户管理应用服务实现。
/// 编排 IUserSessionStore：派生 SessionDurationMs、检测异常会话、强制下线校验。
/// Redis 不可用时 QueryAsync/GetByIdAsync/GetStatsAsync 返回空结果，ForceOfflineAsync 抛 503。
/// </summary>
public sealed class OnlineUserAppService : IOnlineUserAppService
{
    private readonly IUserSessionStore _userSessionStore;
    private readonly ILogger<OnlineUserAppService> _logger;

    public OnlineUserAppService(
        IUserSessionStore userSessionStore,
        ILogger<OnlineUserAppService> logger)
    {
        ArgumentNullException.ThrowIfNull(userSessionStore);
        ArgumentNullException.ThrowIfNull(logger);
        _userSessionStore = userSessionStore;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<OnlineUserListResultDto> QueryAsync(OnlineUserQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        NormalizePaging(query);

        List<OnlineUserSession> sessions;
        try
        {
            sessions = await _userSessionStore.QueryAsync(query, ct);
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex, "Redis 不可用，在线用户查询返回空列表");
            return new OnlineUserListResultDto { Items = new(), Total = 0, Page = query.Page, PageSize = query.PageSize };
        }
        catch (RedisTimeoutException ex)
        {
            _logger.LogWarning(ex, "Redis 超时，在线用户查询返回空列表");
            return new OnlineUserListResultDto { Items = new(), Total = 0, Page = query.Page, PageSize = query.PageSize };
        }

        // 异常会话检测：按 UserId 分组，同 userId 多会话或跨网段标记 IsAnomaly
        var byUser = sessions.Where(s => s.UserId != Guid.Empty).GroupBy(s => s.UserId).ToList();
        var anomalySessionIds = new HashSet<string>();
        foreach (var group in byUser)
        {
            var list = group.ToList();
            if (list.Count >= 2)
            {
                foreach (var s in list)
                {
                    anomalySessionIds.Add(s.SessionId);
                }
                continue;
            }
            // 单会话但跨网段（同 userId 不同会话的 IP /16 前缀不同）—— 单会话场景无法跨段，跳过
        }

        var filtered = sessions.Where(s => MatchesFilter(s, query)).ToList();
        var now = DateTime.UtcNow;
        var dtos = filtered
            .Select(s => ToDto(s, now, anomalySessionIds.Contains(s.SessionId)))
            .OrderByDescending(d => d.LoginAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();

        var total = filtered.Count;

        return new OnlineUserListResultDto
        {
            Items = dtos,
            Total = total,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    /// <inheritdoc />
    public async Task<OnlineUserDto?> GetByIdAsync(string sessionId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return null;
        }

        OnlineUserSession? session;
        try
        {
            session = await _userSessionStore.GetByIdAsync(sessionId, ct);
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex, "Redis 不可用，在线用户详情返回 null");
            return null;
        }

        if (session is null)
        {
            return null;
        }

        return ToDto(session, DateTime.UtcNow, IsAnomaly: false);
    }

    /// <inheritdoc />
    public async Task<OnlineUserStatsDto> GetStatsAsync(CancellationToken ct = default)
    {
        try
        {
            var stats = await _userSessionStore.GetStatsAsync(ct);
            return new OnlineUserStatsDto
            {
                Total = stats.Total,
                Logins24h = stats.Logins24h,
                Anomalies = stats.Anomalies
            };
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex, "Redis 不可用，在线用户统计返回零值");
            return new OnlineUserStatsDto();
        }
    }

    /// <inheritdoc />
    public async Task ForceOfflineAsync(string sessionId, string currentOperatorSessionId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new SystemAdminDomainException("sessionId 不可为空", "ONLINE_USER_SESSION_ID_EMPTY");
        }

        if (!string.IsNullOrEmpty(currentOperatorSessionId)
            && sessionId.Equals(currentOperatorSessionId, StringComparison.Ordinal))
        {
            throw new SystemAdminDomainException("不可强制下线当前操作者自身的会话", "ONLINE_USER_FORCE_OFFLINE_SELF_FORBIDDEN");
        }

        try
        {
            await _userSessionStore.RemoveAsync(sessionId, ct);
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogError(ex, "Redis 不可用，强制下线失败 SessionId={SessionId}", sessionId);
            throw new SystemAdminDomainException("Redis 暂时不可用，强制下线失败", "ONLINE_USER_REDIS_UNAVAILABLE");
        }

        _logger.LogInformation("会话已被强制下线 SessionId={SessionId} OperatorSession={OperatorSession}",
            sessionId, currentOperatorSessionId);
    }

    private static void NormalizePaging(OnlineUserQuery query)
    {
        if (query.Page < 1) query.Page = 1;
        if (query.PageSize < 1) query.PageSize = 20;
        if (query.PageSize > 200) query.PageSize = 200;
    }

    private static bool MatchesFilter(OnlineUserSession s, OnlineUserQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.Username)
            && !s.Username.Contains(query.Username, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        if (!string.IsNullOrWhiteSpace(query.IpAddress)
            && !s.IpAddress.Contains(query.IpAddress, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        if (query.LoginAtFrom.HasValue && s.LoginAt < query.LoginAtFrom.Value)
        {
            return false;
        }
        if (query.LoginAtTo.HasValue && s.LoginAt > query.LoginAtTo.Value)
        {
            return false;
        }
        return true;
    }

    private static OnlineUserDto ToDto(OnlineUserSession s, DateTime now, bool IsAnomaly)
    {
        var durationMs = (long)(now - s.LoginAt).TotalMilliseconds;
        if (durationMs < 0) durationMs = 0;

        return new OnlineUserDto
        {
            SessionId = s.SessionId,
            UserId = s.UserId,
            Username = s.Username,
            Roles = s.Roles.ToList(),
            IpAddress = s.IpAddress,
            GeoLocation = s.GeoLocation,
            Browser = s.Browser,
            Os = s.Os,
            TokenPreview = s.TokenPreview,
            DeviceFingerprint = s.DeviceFingerprint,
            RequestCount = s.RequestCount,
            LoginAt = s.LoginAt,
            LastActivityAt = s.LastActivityAt,
            SessionDurationMs = durationMs,
            IsAnomaly = IsAnomaly
        };
    }
}
