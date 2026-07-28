using Leno.SystemAdmin.Domain.Aggregates;

namespace Leno.SystemAdmin.Domain.ValueObjects;

/// <summary>登录日志查询参数。</summary>
public sealed class LoginLogQuery
{
    public string? Username { get; set; }
    public LoginResult? Result { get; set; }
    public DateTime? LoginAtFrom { get; set; }
    public DateTime? LoginAtTo { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
