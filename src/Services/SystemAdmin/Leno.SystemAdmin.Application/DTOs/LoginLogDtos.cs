using Leno.SystemAdmin.Domain.Aggregates;

namespace Leno.SystemAdmin.Application.DTOs;

/// <summary>
/// 登录日志 DTO，对应前端 spec §3.5。
/// Result 用枚举序列化为字符串（Success/Failed）。
/// </summary>
public sealed class LoginLogDto
{
    public Guid Id { get; set; }

    public string Username { get; set; } = string.Empty;

    public Guid? UserId { get; set; }

    public string IpAddress { get; set; } = string.Empty;

    public string? GeoLocation { get; set; }

    public string Browser { get; set; } = string.Empty;

    public string Os { get; set; } = string.Empty;

    public LoginResult Result { get; set; }

    public string? FailureReason { get; set; }

    public int DurationMs { get; set; }

    public string UserAgent { get; set; } = string.Empty;

    public string? DeviceFingerprint { get; set; }

    public string? RefererUrl { get; set; }

    public string TraceId { get; set; } = string.Empty;

    public DateTime LoginAt { get; set; }
}

/// <summary>登录日志分页查询结果。</summary>
public sealed class LoginLogListResultDto
{
    public List<LoginLogDto> Items { get; set; } = [];

    public int Total { get; set; }

    public int Page { get; set; }

    public int PageSize { get; set; }
}
