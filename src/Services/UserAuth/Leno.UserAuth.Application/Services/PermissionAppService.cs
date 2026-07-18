using Leno.SharedKernel.Abstractions;
using Leno.UserAuth.Application.DTOs;
using Leno.UserAuth.Domain.Aggregates;
using Leno.UserAuth.Domain.Exceptions;
using Leno.UserAuth.Domain.Repositories;
using Leno.UserAuth.Domain.ValueObjects;

namespace Leno.UserAuth.Application.Services;

/// <summary>
/// 角色权限管理应用服务实现。
/// </summary>
public sealed class PermissionAppService : IPermissionAppService
{
    private readonly IPermissionRepository _permissionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public PermissionAppService(
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
    public async Task<RoleDto> GetRoleAsync(Guid roleId, CancellationToken ct = default)
    {
        var role = await RequireRoleAsync(roleId, ct);
        return ToDto(role);
    }

    /// <inheritdoc />
    public async Task<RoleDto> CreateRoleAsync(SaveRoleDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            throw new UserAuthDomainException("角色名称不可为空", "ROLE_NAME_EMPTY");
        }

        // 检查角色名是否已存在
        var existing = await _permissionRepository.GetByNameAsync(dto.Name, ct);
        if (existing is not null)
        {
            throw new UserAuthDomainException("角色名称已存在", "ROLE_NAME_EXISTS");
        }

        var role = Role.Create(Guid.NewGuid(), dto.Name, dto.Description);
        await _permissionRepository.AddAsync(role, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        return ToDto(role);
    }

    /// <inheritdoc />
    public async Task<RoleDto> UpdateRoleAsync(Guid roleId, SaveRoleDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            throw new UserAuthDomainException("角色名称不可为空", "ROLE_NAME_EMPTY");
        }

        var role = await RequireRoleAsync(roleId, ct);

        // 检查名称是否被其他角色占用
        var existing = await _permissionRepository.GetByNameAsync(dto.Name, ct);
        if (existing is not null && existing.Id != roleId)
        {
            throw new UserAuthDomainException("角色名称已存在", "ROLE_NAME_EXISTS");
        }

        role.Update(dto.Name, dto.Description);
        await _permissionRepository.UpdateAsync(role, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        return ToDto(role);
    }

    /// <inheritdoc />
    public async Task DeleteRoleAsync(Guid roleId, CancellationToken ct = default)
    {
        var role = await RequireRoleAsync(roleId, ct);

        if (role.IsBuiltIn)
        {
            throw new UserAuthDomainException("内置角色不可删除", "ROLE_BUILTIN_DELETE");
        }

        // 检查是否有用户引用
        var hasReferences = await _permissionRepository.HasUserReferencesAsync(roleId, ct);
        if (hasReferences)
        {
            throw new UserAuthDomainException("角色存在用户引用，不可删除", "ROLE_HAS_USER_REFERENCES");
        }

        await _permissionRepository.RemoveAsync(role, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<List<string>> GetRolePermissionsAsync(Guid roleId, CancellationToken ct = default)
    {
        var role = await RequireRoleAsync(roleId, ct);
        return role.Permissions.Select(p => p.ResourceKey).ToList();
    }

    /// <inheritdoc />
    public async Task UpdateRolePermissionsAsync(Guid roleId, UpdatePermissionsDto dto, CancellationToken ct = default)
    {
        var role = await RequireRoleAsync(roleId, ct);

        var permissions = dto.Permissions
            .Select(p => new PermissionVO(p))
            .ToList();

        role.SetPermissions(permissions);
        await _permissionRepository.UpdateAsync(role, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    private async Task<Role> RequireRoleAsync(Guid roleId, CancellationToken ct)
    {
        var role = await _permissionRepository.GetByIdAsync(roleId, ct);
        if (role is null)
        {
            throw new UserAuthDomainException("角色不存在", "ROLE_NOT_FOUND");
        }

        return role;
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