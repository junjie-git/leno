namespace Leno.Cart.Application.DTOs;

/// <summary>
/// 合并匿名购物车请求 DTO。
/// </summary>
public sealed class MergeCartRequestDto
{
    /// <summary>匿名会话标识（客户端 Cookie/Header 中携带的 SessionId）。</summary>
    public string AnonymousId { get; init; } = string.Empty;
}