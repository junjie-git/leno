using Leno.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;

namespace Leno.ReviewAfterSales.Api.Controllers;

/// <summary>
/// 需鉴权控制器的基类，提供当前用户标识解析。
/// </summary>
[ApiController]
public abstract class ReviewControllerBase : ControllerBase
{
    protected ICurrentUserContext CurrentUser { get; }

    protected ReviewControllerBase(ICurrentUserContext currentUser)
    {
        ArgumentNullException.ThrowIfNull(currentUser);
        CurrentUser = currentUser;
    }

    /// <summary>解析当前已认证用户标识。</summary>
    protected Guid GetCurrentUserId()
    {
        if (!CurrentUser.IsAuthenticated || !CurrentUser.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("未认证");
        }

        return CurrentUser.UserId.Value;
    }
}
