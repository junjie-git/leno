using Leno.Identity.Domain.ValueObjects;

namespace Leno.Identity.Application.DTOs;

/// <summary>
/// 用户响应 DTO（Identity BC，3.6 AuthN/AuthZ 拆分）。
/// 仅承载身份基本信息，不含角色（角色由 AccessControl BC 通过 GetUserRoles RPC 提供）。
/// </summary>
public sealed class UserDto
{
    /// <summary>用户标识。</summary>
    public Guid Id { get; set; }

    /// <summary>用户名。</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>邮箱（OAuth 注册可空）。</summary>
    public string? Email { get; set; }

    /// <summary>账户状态。</summary>
    public AccountStatus Status { get; set; }

    /// <summary>账户创建时间（UTC）。</summary>
    public DateTime CreatedAt { get; set; }
}
