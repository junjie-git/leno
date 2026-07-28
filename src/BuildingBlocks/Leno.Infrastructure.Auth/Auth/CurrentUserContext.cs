using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Leno.Infrastructure.Auth;

/// <summary>
/// 当前用户上下文抽象，从请求 HttpContext 提取 JWT 声明中的用户信息。
/// 供应用层与基础设施层获取当前操作者。
/// </summary>
public interface ICurrentUserContext
{
    Guid? UserId { get; }

    string? Role { get; }

    Guid? ShopId { get; }

    string? SessionId { get; }

    bool IsAuthenticated { get; }
}

/// <summary>
/// 基于 <see cref="IHttpContextAccessor"/> 的当前用户上下文实现。
/// </summary>
public sealed class CurrentUserContext : ICurrentUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserContext(IHttpContextAccessor httpContextAccessor)
    {
        ArgumentNullException.ThrowIfNull(httpContextAccessor);
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public Guid? UserId => JwtTokenGenerator.GetUserId(User);

    public string? Role => JwtTokenGenerator.GetRole(User);

    public Guid? ShopId => JwtTokenGenerator.GetShopId(User);

    public string? SessionId => JwtTokenGenerator.GetSessionId(User);
}
