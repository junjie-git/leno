using Leno.Identity.Domain.ValueObjects;

namespace Leno.Identity.Application.DTOs;

/// <summary>
/// 管理后台用户列表项 DTO（Identity BC）。
/// 角色信息由 AccessControl BC 维护，本 DTO 的 Roles 字段为空集合（需通过 AccessControl RPC 填充）。
/// 从 UserAuth BC 迁入 Identity BC（3.6 AuthN/AuthZ 拆分）。
/// </summary>
public sealed class AdminUserDto
{
    public Guid Id { get; init; }

    public string Username { get; init; } = string.Empty;

    public string? Email { get; init; }

    public string? PhoneNumber { get; init; }

    public string Nickname { get; init; } = string.Empty;

    public AccountStatus Status { get; init; }

    /// <summary>角色编码集合。Identity BC 不持久化角色，此处默认空集合，由 Controller 层调 AccessControl RPC 填充。</summary>
    public IReadOnlyCollection<string> Roles { get; init; } = Array.Empty<string>();

    public int FailedLoginCount { get; init; }

    public DateTime? LockedUntil { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime UpdatedAt { get; init; }
}

/// <summary>
/// 管理后台用户分页查询参数（Identity BC）。
/// </summary>
public sealed class AdminUserQueryDto
{
    /// <summary>关键词（用户名/昵称模糊匹配）。</summary>
    public string? Keyword { get; init; }

    /// <summary>状态过滤（Active/Locked/Disabled）。</summary>
    public string? Status { get; init; }

    /// <summary>页码，从 1 起。</summary>
    public int Page { get; init; } = 1;

    /// <summary>每页大小，默认 20，最大 100。</summary>
    public int PageSize { get; init; } = 20;
}

/// <summary>
/// 管理员锁定账户请求 DTO（Identity BC）。
/// </summary>
public sealed class SuspendUserDto
{
    /// <summary>锁定原因。</summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>锁定时长（分钟），默认 30 分钟。</summary>
    public int DurationMinutes { get; init; } = 30;
}
