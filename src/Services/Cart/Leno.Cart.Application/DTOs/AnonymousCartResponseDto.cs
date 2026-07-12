namespace Leno.Cart.Application.DTOs;

/// <summary>
/// 匿名购物车创建响应，包含会话标识与购物车数据。
/// </summary>
public sealed class AnonymousCartResponseDto
{
    /// <summary>会话标识，客户端后续请求需携带此值。</summary>
    public string SessionId { get; init; } = string.Empty;

    /// <summary>购物车数据。</summary>
    public CartDto Cart { get; init; } = null!;
}