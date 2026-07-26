using Leno.AccessControl.Domain.Aggregates;
using Leno.AccessControl.Domain.Exceptions;
using Leno.AccessControl.Domain.Repositories;
using Leno.AccessControl.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.AccessControl.Application.Services;

/// <summary>
/// 角色权限管理应用服务实现。
/// 从 UserAuth BC AdminRolesController 后置应用服务迁移而来，去除对 operatorId 审计参数的依赖，
/// 权限列表类型统一为 <see cref="IReadOnlyList{T}"/>。
/// <see cref="GetRolePermissionsAsync"/> 在角色不存在时返回空列表（由 Controller 层先验证角色存在性，
/// 或允许返回空列表表示"无权限"语义）；<see cref="UpdateRolePermissionsAsync"/> 在角色不存在时抛异常。
/// </summary>
public sealed class RolePermissionAppService : IRolePermissionAppService
{
    private readonly IPermissionRepository _permissionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RolePermissionAppService(
        IPermissionRepository permissionRepository,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(permissionRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        _permissionRepository = permissionRepository;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetRolePermissionsAsync(Guid roleId, CancellationToken ct = default)
    {
        if (roleId == Guid.Empty)
        {
            return Array.Empty<string>();
        }

        var role = await _permissionRepository.GetByIdAsync(roleId, ct);
        if (role is null)
        {
            return Array.Empty<string>();
        }

        return role.Permissions.Select(p => p.ResourceKey).ToList();
    }

    /// <inheritdoc />
    public async Task UpdateRolePermissionsAsync(Guid roleId, IReadOnlyList<string> permissions, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        if (roleId == Guid.Empty)
        {
            throw new AccessControlDomainException("角色标识不可为空", "ROLE_ID_EMPTY");
        }

        var role = await RequireRoleAsync(roleId, ct);

        // 预校验所有权限资源键格式（PermissionVO 构造函数会抛 ArgumentException，
        // 这里转换为领域异常，由全局异常中间件统一映射为业务错误响应）
        var permissionVOs = new List<PermissionVO>(permissions.Count);
        foreach (var raw in permissions)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                throw new AccessControlDomainException("权限资源键不可为空", "PERMISSION_KEY_EMPTY");
            }

            PermissionVO permission;
            try
            {
                permission = new PermissionVO(raw);
            }
            catch (ArgumentException ex)
            {
                throw new AccessControlDomainException(ex.Message, "PERMISSION_KEY_INVALID");
            }

            // 幂等去重：同一 resourceKey 仅保留首次出现
            if (!permissionVOs.Any(p => p.ResourceKey == permission.ResourceKey))
            {
                permissionVOs.Add(permission);
            }
        }

        role.SetPermissions(permissionVOs);
        await _permissionRepository.UpdateAsync(role, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    private async Task<Role> RequireRoleAsync(Guid roleId, CancellationToken ct)
    {
        var role = await _permissionRepository.GetByIdAsync(roleId, ct);
        if (role is null)
        {
            throw new AccessControlDomainException("角色不存在", "ROLE_NOT_FOUND");
        }

        return role;
    }
}
