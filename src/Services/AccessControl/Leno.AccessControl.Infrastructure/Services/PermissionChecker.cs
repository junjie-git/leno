using Leno.AccessControl.Domain.Repositories;
using Leno.AccessControl.Domain.Services;
using Leno.AccessControl.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Leno.AccessControl.Infrastructure.Services;

/// <summary>
/// 权限校验领域服务实现。
/// 校验逻辑：
/// 1. 查询用户当前生效的角色编码列表（带 5 分钟 IMemoryCache 缓存，避免每次 RPC 都查库）。
/// 2. 按角色名称加载对应 Role 聚合，检查是否拥有指定 resourceKey 权限。
/// 3. 任一角色拥有该权限即放行；否则拒绝。
/// 缓存策略：角色变更时由应用服务显式失效（通过 <see cref="InvalidateUserCache"/>）。
/// </summary>
public sealed class PermissionChecker : IPermissionChecker
{
    /// <summary>用户角色缓存有效期：5 分钟，与 JWT 角色填充缓存一致。</summary>
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    private readonly IUserRoleAssignmentRepository _userRoleRepository;
    private readonly IPermissionRepository _permissionRepository;
    private readonly IMemoryCache _cache;

    public PermissionChecker(
        IUserRoleAssignmentRepository userRoleRepository,
        IPermissionRepository permissionRepository,
        IMemoryCache cache)
    {
        ArgumentNullException.ThrowIfNull(userRoleRepository);
        ArgumentNullException.ThrowIfNull(permissionRepository);
        ArgumentNullException.ThrowIfNull(cache);
        _userRoleRepository = userRoleRepository;
        _permissionRepository = permissionRepository;
        _cache = cache;
    }

    /// <inheritdoc />
    public async Task<PermissionCheckResult> CheckPermissionAsync(
        Guid userId,
        string resource,
        string? action = null,
        CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
        {
            return PermissionCheckResult.Deny("用户标识不可为空");
        }

        if (string.IsNullOrWhiteSpace(resource))
        {
            return PermissionCheckResult.Deny("资源键不可为空");
        }

        // 1. 获取用户生效角色编码列表（带缓存）
        var roleCodes = await GetCachedRoleCodesAsync(userId, ct);
        if (roleCodes.Count == 0)
        {
            return PermissionCheckResult.Deny("用户未分配任何角色");
        }

        // 2. 逐角色检查权限
        var matchedPolicies = new List<string>();
        foreach (var roleCode in roleCodes)
        {
            var role = await _permissionRepository.GetByNameAsync(roleCode, ct);
            if (role is null)
            {
                continue;
            }

            if (role.HasPermission(resource))
            {
                matchedPolicies.Add($"role:{roleCode}:permission:{resource}");
            }
        }

        if (matchedPolicies.Count > 0)
        {
            return PermissionCheckResult.Allow(matchedPolicies);
        }

        return PermissionCheckResult.Deny($"用户角色均未授权访问资源 {resource}", matchedPolicies);
    }

    /// <summary>
    /// 失效指定用户的角色缓存（角色变更时由应用服务调用）。
    /// </summary>
    public void InvalidateUserCache(Guid userId)
    {
        var cacheKey = BuildCacheKey(userId);
        _cache.Remove(cacheKey);
    }

    private async Task<IReadOnlyList<string>> GetCachedRoleCodesAsync(Guid userId, CancellationToken ct)
    {
        var cacheKey = BuildCacheKey(userId);
        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<string>? cached) && cached is not null)
        {
            return cached;
        }

        var roleCodes = await _userRoleRepository.GetActiveRoleCodesAsync(userId, ct);
        var entries = roleCodes.ToList();
        _cache.Set(cacheKey, entries, CacheDuration);
        return entries;
    }

    private static string BuildCacheKey(Guid userId)
        => $"permission:checker:user:roles:{userId}";
}
