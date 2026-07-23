namespace Leno.AccessControl.Domain.Services;

/// <summary>
/// 权限校验结果，承载是否放行、匹配策略与拒绝原因。
/// </summary>
public sealed record PermissionCheckResult
{
    /// <summary>是否放行。</summary>
    public bool Allowed { get; init; }

    /// <summary>匹配的策略列表（角色编码或权限资源键），用于审计与调试。</summary>
    public IReadOnlyList<string> MatchedPolicies { get; init; } = Array.Empty<string>();

    /// <summary>拒绝原因，放行时为空。</summary>
    public string DenialReason { get; init; } = string.Empty;

    public static PermissionCheckResult Allow(IReadOnlyList<string>? matchedPolicies = null)
        => new()
        {
            Allowed = true,
            MatchedPolicies = matchedPolicies ?? Array.Empty<string>(),
            DenialReason = string.Empty
        };

    public static PermissionCheckResult Deny(string reason, IReadOnlyList<string>? matchedPolicies = null)
        => new()
        {
            Allowed = false,
            MatchedPolicies = matchedPolicies ?? Array.Empty<string>(),
            DenialReason = reason ?? string.Empty
        };
}

/// <summary>
/// 权限校验领域服务抽象，定义在领域层，由基础设施层实现。
/// 校验用户是否拥有指定资源与操作的权限，供 AccessControlGrpcService.CheckPermission RPC 调用。
/// 实现基于角色权限集合与用户角色分配关系联合查询。
/// </summary>
public interface IPermissionChecker
{
    /// <summary>
    /// 校验用户是否拥有指定资源与操作的权限。
    /// </summary>
    /// <param name="userId">用户标识。</param>
    /// <param name="resource">资源键，格式 api:/path 或 ui:module:action。</param>
    /// <param name="action">操作（read/write/delete），当前实现以 resource 隐含 action，此参数保留扩展。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>权限校验结果。</returns>
    Task<PermissionCheckResult> CheckPermissionAsync(
        Guid userId,
        string resource,
        string? action = null,
        CancellationToken ct = default);
}
