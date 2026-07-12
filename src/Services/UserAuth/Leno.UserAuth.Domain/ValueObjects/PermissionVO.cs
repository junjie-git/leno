namespace Leno.UserAuth.Domain.ValueObjects;

/// <summary>
/// 权限值对象，表示某个资源与操作的组合。
/// ResourceKey 格式：<c>api:/path</c> 表示 API 权限，<c>ui:module:action</c> 表示 UI 权限。
/// </summary>
public sealed record PermissionVO
{
    /// <summary>权限资源键，格式：api:/path 或 ui:module:action。</summary>
    public string ResourceKey { get; }

    /// <summary>权限描述。</summary>
    public string? Description { get; init; }

    public PermissionVO(string resourceKey)
    {
        if (string.IsNullOrWhiteSpace(resourceKey))
        {
            throw new ArgumentException("权限资源键不可为空", nameof(resourceKey));
        }

        var trimmed = resourceKey.Trim();
        if (!trimmed.Contains(':'))
        {
            throw new ArgumentException("权限资源键格式须为 type:resource（如 api:/path 或 ui:module:action）", nameof(resourceKey));
        }

        ResourceKey = trimmed;
    }

    /// <summary>判断是否为 API 权限。</summary>
    public bool IsApiPermission => ResourceKey.StartsWith("api:", StringComparison.OrdinalIgnoreCase);

    /// <summary>判断是否为 UI 权限。</summary>
    public bool IsUiPermission => ResourceKey.StartsWith("ui:", StringComparison.OrdinalIgnoreCase);

    public override string ToString() => ResourceKey;
}