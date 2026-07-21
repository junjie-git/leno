namespace Leno.Infrastructure.Auth;

/// <summary>
/// 资源归属校验器，统一处理 IDOR 越权防护。
/// 所有按资源 ID 查询/操作的端点应调用 EnsureOwnerAsync 校验当前用户是否为资源所有者。
/// </summary>
public sealed class ResourceOwnershipChecker
{
    private readonly ICurrentUserContext _userContext;

    public ResourceOwnershipChecker(ICurrentUserContext userContext)
    {
        _userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));
    }

    /// <summary>
    /// 校验当前用户是否为资源所有者，不是则抛 <see cref="ForbiddenAccessException"/>。
    /// 未认证用户抛 <see cref="UnauthorizedAccessException"/>。
    /// </summary>
    /// <param name="resourceOwnerId">资源所有者的 UserId。</param>
    /// <param name="resourceType">资源类型名称（用于错误提示，如 "ORDER"、"REVIEW"）。</param>
    /// <exception cref="UnauthorizedAccessException">当前用户未认证时抛出。</exception>
    /// <exception cref="ForbiddenAccessException">当前用户非资源所有者时抛出。</exception>
    public Task EnsureOwnerAsync(Guid resourceOwnerId, string resourceType)
    {
        if (!_userContext.IsAuthenticated || _userContext.UserId is null)
        {
            throw new UnauthorizedAccessException("用户未认证");
        }

        if (_userContext.UserId.Value != resourceOwnerId)
        {
            throw new ForbiddenAccessException(resourceType);
        }

        return Task.CompletedTask;
    }
}
