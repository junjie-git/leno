namespace Leno.UserAuth.Application.DTOs;

/// <summary>
/// 管理员锁定账户请求 DTO。
/// </summary>
public sealed class SuspendUserDto
{
    /// <summary>锁定原因。</summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>锁定时长（分钟），默认 30 分钟。</summary>
    public int DurationMinutes { get; init; } = 30;
}
