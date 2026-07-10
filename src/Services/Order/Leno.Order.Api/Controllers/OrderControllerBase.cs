using Leno.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;

namespace Leno.Order.Api.Controllers;

/// <summary>
/// 需鉴权控制器的基类，提供当前用户标识解析。
/// 派生控制器通过 <see cref="GetCurrentUserId"/> 获取 JWT 声明中的用户标识。
/// </summary>
[ApiController]
public abstract class OrderControllerBase : ControllerBase
{
    protected ICurrentUserContext CurrentUser { get; }

    protected OrderControllerBase(ICurrentUserContext currentUser)
    {
        ArgumentNullException.ThrowIfNull(currentUser);
        CurrentUser = currentUser;
    }

    /// <summary>解析当前已认证用户标识，未认证时抛出 <see cref="UnauthorizedAccessException"/>（映射 401）。</summary>
    protected Guid GetCurrentUserId()
    {
        if (!CurrentUser.IsAuthenticated || !CurrentUser.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("未认证");
        }

        return CurrentUser.UserId.Value;
    }
}
