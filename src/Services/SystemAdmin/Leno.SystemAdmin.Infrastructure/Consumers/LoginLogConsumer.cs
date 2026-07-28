using Leno.Infrastructure.Abstractions.Geo;
using Leno.Infrastructure.Abstractions.UserAgent;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Leno.SystemAdmin.Infrastructure.Consumers;

/// <summary>
/// 用户登录事件消费者：消费 UserLoggedInEvent 持久化为 LoginLog 聚合。
/// 幂等去重：按 EventId 检查已存在则跳过；UA 解析与地理定位在消费侧完成。
/// 失败登录事件同样记录（Success=false + FailureReason）。
/// </summary>
public sealed class LoginLogConsumer : IConsumer<UserLoggedInEvent>
{
    private readonly ILoginLogRepository _loginLogRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserAgentParser _uaParser;
    private readonly IGeoLocationResolver _geoResolver;
    private readonly ILogger<LoginLogConsumer> _logger;

    public LoginLogConsumer(
        ILoginLogRepository loginLogRepository,
        IUnitOfWork unitOfWork,
        IUserAgentParser uaParser,
        IGeoLocationResolver geoResolver,
        ILogger<LoginLogConsumer> logger)
    {
        ArgumentNullException.ThrowIfNull(loginLogRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(uaParser);
        ArgumentNullException.ThrowIfNull(geoResolver);
        ArgumentNullException.ThrowIfNull(logger);
        _loginLogRepository = loginLogRepository;
        _unitOfWork = unitOfWork;
        _uaParser = uaParser;
        _geoResolver = geoResolver;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<UserLoggedInEvent> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var evt = context.Message;
        var ct = context.CancellationToken;

        // 幂等去重：按 EventId 检查是否已存在（快速路径）
        var existing = await _loginLogRepository.GetByEventIdAsync(evt.EventId, ct);
        if (existing is not null)
        {
            _logger.LogDebug("登录日志已存在，跳过 EventId={EventId}", evt.EventId);
            return;
        }

        // UA 解析：空 UA 兜底为 "Unknown"（Create 方法对空字符串会抛异常）
        var userAgent = string.IsNullOrWhiteSpace(evt.UserAgent) ? "Unknown" : evt.UserAgent;
        var browser = _uaParser.ParseBrowser(userAgent);
        var os = _uaParser.ParseOs(userAgent);
        var deviceFingerprint = _uaParser.ParseDeviceFingerprint(userAgent);

        // 地理定位：空 IP 视为无法解析，返回 null
        var geo = string.IsNullOrWhiteSpace(evt.IpAddress) ? null : _geoResolver.Resolve(evt.IpAddress);
        var geoLocation = geo is null
                          || (string.IsNullOrEmpty(geo.Country)
                              && string.IsNullOrEmpty(geo.Province)
                              && string.IsNullOrEmpty(geo.City))
            ? null
            : geo.ToString();

        // LoginAt 兜底：事件未携带时间戳时使用当前 UTC
        var loginAt = evt.OccurredAt == default ? DateTime.UtcNow : evt.OccurredAt;

        var logId = Guid.NewGuid();
        var loginLog = evt.Success
            ? LoginLog.CreateSuccess(
                logId,
                evt.Username,
                evt.UserId ?? Guid.Empty,
                evt.IpAddress,
                browser,
                os,
                userAgent,
                evt.TraceId,
                evt.DurationMs,
                loginAt,
                eventId: evt.EventId,
                geoLocation: geoLocation,
                deviceFingerprint: deviceFingerprint,
                refererUrl: evt.RefererUrl)
            : LoginLog.CreateFailed(
                logId,
                evt.Username,
                evt.IpAddress,
                browser,
                os,
                userAgent,
                evt.TraceId,
                evt.DurationMs,
                evt.FailureReason ?? "未知原因",
                loginAt,
                eventId: evt.EventId,
                geoLocation: geoLocation,
                deviceFingerprint: deviceFingerprint,
                refererUrl: evt.RefererUrl);

        try
        {
            await _loginLogRepository.AddAsync(loginLog, ct);
            await _unitOfWork.SaveEntitiesAsync(ct);

            _logger.LogInformation("登录日志已记录 EventId={EventId} Username={Username} Success={Success}",
                evt.EventId, evt.Username, evt.Success);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // 并发插入导致唯一索引冲突，视为已存在，正常返回
            _logger.LogWarning(ex,
                "登录日志并发插入冲突，已按幂等处理 EventId={EventId}", evt.EventId);
        }
    }

    /// <summary>
    /// 判断 DbUpdateException 是否为唯一约束冲突（SQL Server 错误码 2601/2627），
    /// 兼容 PostgreSQL/MySQL 的错误消息。
    /// </summary>
    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        var inner = ex.InnerException;
        if (inner is null)
        {
            return false;
        }

        var message = inner.Message ?? string.Empty;
        return message.Contains("2601", StringComparison.Ordinal)
            || message.Contains("2627", StringComparison.Ordinal)
            || message.Contains("ix_login_logs_event_id", StringComparison.OrdinalIgnoreCase)
            || message.Contains("UNIQUE KEY", StringComparison.OrdinalIgnoreCase)
            || message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Duplicate entry", StringComparison.OrdinalIgnoreCase);
    }
}
