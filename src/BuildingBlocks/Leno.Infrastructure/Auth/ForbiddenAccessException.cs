namespace Leno.Infrastructure.Auth;

/// <summary>
/// 当用户尝试访问不属于自己的资源时抛出，对应 HTTP 403 Forbidden。
/// 错误消息不暴露资源所有者标识，防止信息泄露。
/// </summary>
public sealed class ForbiddenAccessException : Exception
{
    /// <summary>
    /// 资源类型名称（如 "ORDER"、"REVIEW"），用于错误提示与日志分类。
    /// </summary>
    public string ResourceType { get; }

    public ForbiddenAccessException(string resourceType)
        : base($"当前用户无权访问该 {resourceType} 资源")
    {
        ResourceType = resourceType;
    }
}
