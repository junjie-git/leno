namespace Leno.Infrastructure.Abstractions.Sessions;

/// <summary>在线用户查询参数。</summary>
public sealed class OnlineUserQuery
{
    public string? Username { get; set; }
    public string? IpAddress { get; set; }
    public DateTime? LoginAtFrom { get; set; }
    public DateTime? LoginAtTo { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
