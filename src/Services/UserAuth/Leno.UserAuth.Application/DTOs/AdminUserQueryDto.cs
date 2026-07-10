namespace Leno.UserAuth.Application.DTOs;

/// <summary>
/// 管理后台用户分页查询参数。
/// </summary>
public sealed class AdminUserQueryDto
{
    /// <summary>关键词（用户名/昵称模糊匹配）。</summary>
    public string? Keyword { get; init; }

    /// <summary>角色过滤（Buyer/Seller/Operator/Admin）。</summary>
    public string? Role { get; init; }

    /// <summary>状态过滤（Active/Locked/Disabled）。</summary>
    public string? Status { get; init; }

    /// <summary>页码，从 1 起。</summary>
    public int Page { get; init; } = 1;

    /// <summary>每页大小，默认 20，最大 100。</summary>
    public int PageSize { get; init; } = 20;
}
