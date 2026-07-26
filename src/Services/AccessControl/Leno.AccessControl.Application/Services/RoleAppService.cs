using Leno.AccessControl.Application.DTOs;
using Leno.AccessControl.Domain.Aggregates;
using Leno.AccessControl.Domain.Exceptions;
using Leno.AccessControl.Domain.Repositories;
using Leno.SharedKernel.Abstractions;

namespace Leno.AccessControl.Application.Services;

/// <summary>
/// 角色管理应用服务实现，承载角色 CRUD 业务逻辑。
/// 从 UserAuth BC AdminRolesController 后置应用服务迁移而来，去除对 operatorId 审计参数的依赖
/// （审计由 SystemAdmin BC 消费 <c>UserRoleAssignedEvent</c>/<c>UserRoleRevokedEvent</c> 等领域事件完成）。
/// 与 <see cref="PermissionAppService"/> 区别：本实现面向 HTTP Controller，<see cref="GetRoleAsync"/>
/// 返回 null 由 Controller 决定 404 语义，<see cref="UpdateRoleAsync"/>/<see cref="DeleteRoleAsync"/>
/// 在角色不存在时仍抛 <see cref="AccessControlDomainException"/>，由全局异常中间件统一映射。
/// </summary>
public sealed class RoleAppService : IRoleAppService
{
    private readonly IPermissionRepository _permissionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RoleAppService(
        IPermissionRepository permissionRepository,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(permissionRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        _permissionRepository = permissionRepository;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task<PagedResult<RoleDto>> QueryRolesAsync(
        string? keyword, int page, int pageSize, CancellationToken ct = default)
    {
        var (items, total) = await _permissionRepository.QueryAsync(keyword, page, pageSize, ct);
        var dtos = items.Select(ToDto).ToList();

        return new PagedResult<RoleDto>
        {
            Items = dtos,
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <inheritdoc />
    public async Task<RoleDto?> GetRoleAsync(Guid roleId, CancellationToken ct = default)
    {
        if (roleId == Guid.Empty)
        {
            return null;
        }

        var role = await _permissionRepository.GetByIdAsync(roleId, ct);
        return role is null ? null : ToDto(role);
    }

    /// <inheritdoc />
    public async Task<RoleDto> CreateRoleAsync(CreateRoleDto request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateName(request.Name);

        // 检查角色名是否已存在
        var existing = await _permissionRepository.GetByNameAsync(request.Name, ct);
        if (existing is not null)
        {
            throw new AccessControlDomainException("角色名称已存在", "ROLE_NAME_EXISTS");
        }

        var role = Role.Create(Guid.NewGuid(), request.Name, request.Description);
        await _permissionRepository.AddAsync(role, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        return ToDto(role);
    }

    /// <inheritdoc />
    public async Task UpdateRoleAsync(Guid roleId, UpdateRoleDto request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (roleId == Guid.Empty)
        {
            throw new AccessControlDomainException("角色标识不可为空", "ROLE_ID_EMPTY");
        }
        ValidateName(request.Name);

        var role = await RequireRoleAsync(roleId, ct);

        // 检查名称是否被其他角色占用
        var existing = await _permissionRepository.GetByNameAsync(request.Name, ct);
        if (existing is not null && existing.Id != roleId)
        {
            throw new AccessControlDomainException("角色名称已存在", "ROLE_NAME_EXISTS");
        }

        role.Update(request.Name, request.Description);
        await _permissionRepository.UpdateAsync(role, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    /// <inheritdoc />
    public async Task DeleteRoleAsync(Guid roleId, CancellationToken ct = default)
    {
        if (roleId == Guid.Empty)
        {
            throw new AccessControlDomainException("角色标识不可为空", "ROLE_ID_EMPTY");
        }

        var role = await RequireRoleAsync(roleId, ct);

        if (role.IsBuiltIn)
        {
            throw new AccessControlDomainException("内置角色不可删除", "ROLE_BUILTIN_DELETE");
        }

        // 检查是否有用户引用
        var hasReferences = await _permissionRepository.HasUserReferencesAsync(roleId, ct);
        if (hasReferences)
        {
            throw new AccessControlDomainException("角色存在用户引用，不可删除", "ROLE_HAS_USER_REFERENCES");
        }

        await _permissionRepository.RemoveAsync(role, ct);
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

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new AccessControlDomainException("角色名称不可为空", "ROLE_NAME_EMPTY");
        }

        if (name.Trim().Length is < 2 or > 64)
        {
            throw new AccessControlDomainException("角色名称长度须为 2-64 字符", "ROLE_NAME_LENGTH");
        }
    }

    private static RoleDto ToDto(Role role)
        => new()
        {
            Id = role.Id,
            Name = role.Name,
            Description = role.Description,
            IsBuiltIn = role.IsBuiltIn,
            Permissions = role.Permissions.Select(p => p.ResourceKey).ToList(),
            CreatedAt = role.CreatedAt,
            UpdatedAt = role.UpdatedAt
        };
}
