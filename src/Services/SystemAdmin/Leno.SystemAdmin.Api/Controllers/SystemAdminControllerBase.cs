using Leno.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;

namespace Leno.SystemAdmin.Api.Controllers;

/// <summary>
/// 系统管理域控制器基类，提供当前操作者标识解析。
/// </summary>
[ApiController]
public abstract class SystemAdminControllerBase : ControllerBase
{
    protected ICurrentUserContext CurrentUser { get; }

    protected SystemAdminControllerBase(ICurrentUserContext currentUser)
    {
        ArgumentNullException.ThrowIfNull(currentUser);
        CurrentUser = currentUser;
    }

    /// <summary>解析当前已认证操作者标识。</summary>
    protected Guid GetCurrentOperatorId()
    {
        if (!CurrentUser.IsAuthenticated || !CurrentUser.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("未认证");
        }

        return CurrentUser.UserId.Value;
    }
}
