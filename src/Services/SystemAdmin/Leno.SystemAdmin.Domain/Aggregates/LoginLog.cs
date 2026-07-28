using Leno.SystemAdmin.Domain.Exceptions;
using Leno.SharedKernel.Abstractions;

namespace Leno.SystemAdmin.Domain.Aggregates;

/// <summary>
/// 登录日志聚合根：仅追加（Append-Only），登录成功或失败时由消费者写入。
/// 与 AuditLog 解耦：AuditLog 记录运营操作，LoginLog 专记认证事件。
/// </summary>
public sealed class LoginLog : AggregateRoot
{
    private const int MaxUsernameLength = 64;
    private const int MaxIpLength = 64;
    private const int MaxGeoLength = 128;
    private const int MaxBrowserLength = 64;
    private const int MaxOsLength = 64;
    private const int MaxFailureReasonLength = 64;
    private const int MaxUserAgentLength = 512;
    private const int MaxDeviceFingerprintLength = 128;
    private const int MaxRefererLength = 512;
    private const int MaxTraceIdLength = 64;

    public string Username { get; private set; } = string.Empty;
    public Guid? UserId { get; private set; }
    public string IpAddress { get; private set; } = string.Empty;
    public string? GeoLocation { get; private set; }
    public string Browser { get; private set; } = string.Empty;
    public string Os { get; private set; } = string.Empty;
    public LoginResult Result { get; private set; }
    public string? FailureReason { get; private set; }
    public int DurationMs { get; private set; }
    public string UserAgent { get; private set; } = string.Empty;
    public string? DeviceFingerprint { get; private set; }
    public string? RefererUrl { get; private set; }
    public string TraceId { get; private set; } = string.Empty;
    public Guid EventId { get; private set; }
    public DateTime LoginAt { get; private set; }

    private LoginLog() { }

    private LoginLog(Guid id) : base(id) { }

    public static LoginLog CreateSuccess(
        Guid logId,
        string username,
        Guid userId,
        string ipAddress,
        string browser,
        string os,
        string userAgent,
        string traceId,
        int durationMs,
        DateTime loginAt,
        Guid eventId = default,
        string? geoLocation = null,
        string? deviceFingerprint = null,
        string? refererUrl = null,
        string? failureReason = null)
    {
        if (!string.IsNullOrWhiteSpace(failureReason))
        {
            throw new SystemAdminDomainException("成功登录不可填写 FailureReason", "LOGIN_SUCCESS_WITH_REASON");
        }
        return Create(logId, username, userId, ipAddress, browser, os, userAgent, traceId, eventId,
            durationMs, loginAt, LoginResult.Success, failureReason: null,
            geoLocation, deviceFingerprint, refererUrl);
    }

    public static LoginLog CreateFailed(
        Guid logId,
        string username,
        string ipAddress,
        string browser,
        string os,
        string userAgent,
        string traceId,
        int durationMs,
        string failureReason,
        DateTime loginAt,
        Guid eventId = default,
        string? geoLocation = null,
        string? deviceFingerprint = null,
        string? refererUrl = null)
    {
        if (string.IsNullOrWhiteSpace(failureReason))
        {
            throw new SystemAdminDomainException("失败登录必须填写 FailureReason", "LOGIN_FAILED_REASON_REQUIRED");
        }
        return Create(logId, username, userId: null, ipAddress, browser, os, userAgent, traceId, eventId,
            durationMs, loginAt, LoginResult.Failed, failureReason,
            geoLocation, deviceFingerprint, refererUrl);
    }

    private static LoginLog Create(
        Guid logId,
        string username,
        Guid? userId,
        string ipAddress,
        string browser,
        string os,
        string userAgent,
        string traceId,
        Guid eventId,
        int durationMs,
        DateTime loginAt,
        LoginResult result,
        string? failureReason,
        string? geoLocation,
        string? deviceFingerprint,
        string? refererUrl)
    {
        if (logId == Guid.Empty)
        {
            throw new SystemAdminDomainException("日志标识不可为空", "LOGIN_LOG_ID_EMPTY");
        }
        ValidateString(username, MaxUsernameLength, "用户名", "LOGIN_USERNAME");
        ValidateString(ipAddress, MaxIpLength, "IP 地址", "LOGIN_IP");
        ValidateString(browser, MaxBrowserLength, "浏览器", "LOGIN_BROWSER");
        ValidateString(os, MaxOsLength, "操作系统", "LOGIN_OS");
        ValidateString(userAgent, MaxUserAgentLength, "UserAgent", "LOGIN_UA");
        ValidateString(traceId, MaxTraceIdLength, "TraceId", "LOGIN_TRACE");
        if (durationMs < 0)
        {
            throw new SystemAdminDomainException("DurationMs 不可为负数", "LOGIN_DURATION_NEGATIVE");
        }
        if (!string.IsNullOrWhiteSpace(failureReason) && failureReason.Trim().Length > MaxFailureReasonLength)
        {
            throw new SystemAdminDomainException($"FailureReason 长度不可超过 {MaxFailureReasonLength} 字符", "LOGIN_REASON_LENGTH");
        }
        if (!string.IsNullOrWhiteSpace(geoLocation) && geoLocation.Trim().Length > MaxGeoLength)
        {
            throw new SystemAdminDomainException($"GeoLocation 长度不可超过 {MaxGeoLength} 字符", "LOGIN_GEO_LENGTH");
        }
        if (!string.IsNullOrWhiteSpace(deviceFingerprint) && deviceFingerprint.Trim().Length > MaxDeviceFingerprintLength)
        {
            throw new SystemAdminDomainException($"DeviceFingerprint 长度不可超过 {MaxDeviceFingerprintLength} 字符", "LOGIN_DEVICE_LENGTH");
        }
        if (!string.IsNullOrWhiteSpace(refererUrl) && refererUrl.Trim().Length > MaxRefererLength)
        {
            throw new SystemAdminDomainException($"RefererUrl 长度不可超过 {MaxRefererLength} 字符", "LOGIN_REFERER_LENGTH");
        }
        if (loginAt == default)
        {
            throw new SystemAdminDomainException("LoginAt 不可为空", "LOGIN_AT_EMPTY");
        }

        return new LoginLog(logId)
        {
            Username = username.Trim(),
            UserId = userId,
            IpAddress = ipAddress.Trim(),
            Browser = browser.Trim(),
            Os = os.Trim(),
            UserAgent = userAgent.Trim(),
            TraceId = traceId.Trim(),
            EventId = eventId,
            DurationMs = durationMs,
            LoginAt = loginAt,
            Result = result,
            FailureReason = NormalizeNullable(failureReason),
            GeoLocation = NormalizeNullable(geoLocation),
            DeviceFingerprint = NormalizeNullable(deviceFingerprint),
            RefererUrl = NormalizeNullable(refererUrl),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static void ValidateString(string value, int maxLength, string fieldName, string errorCodePrefix)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new SystemAdminDomainException($"{fieldName}不可为空", $"{errorCodePrefix}_EMPTY");
        }
        if (value.Trim().Length > maxLength)
        {
            throw new SystemAdminDomainException($"{fieldName}长度不可超过 {maxLength} 字符", $"{errorCodePrefix}_LENGTH");
        }
    }

    private static string? NormalizeNullable(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
