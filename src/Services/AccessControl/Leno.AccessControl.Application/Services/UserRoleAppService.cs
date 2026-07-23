using Leno.AccessControl.Domain.Aggregates;
using Leno.AccessControl.Domain.Exceptions;
using Leno.AccessControl.Domain.Repositories;
using Leno.AccessControl.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.AccessControl.Application.Services;

/// <summary>
/// 用户角色分配应用服务实现。
/// 从 UserAuth BC 的 UserAdminAppService 角色分配职责拆出（3.6 AuthN/AuthZ 拆分）。
/// 承载 INV-12（至少保留一个角色）与 INV-13（禁止撤销自身 Admin）不变式校验。
/// </summary>
public sealed class UserRoleAppService : IUserRoleAppService
{
    private readonly IUserRoleAssignmentRepository _userRoleRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UserRoleAppService(
        IUserRoleAssignmentRepository userRoleRepository,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(userRoleRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        _userRoleRepository = userRoleRepository;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task<List<string>> GetUserRolesAsync(Guid userId, CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
        {
            throw new AccessControlDomainException("用户标识不可为空", "USER_ROLE_USER_EMPTY");
        }

        var roleCodes = await _userRoleRepository.GetActiveRoleCodesAsync(userId, ct);
        return roleCodes.ToList();
    }

    /// <inheritdoc />
    public async Task AssignRoleAsync(Guid userId, RoleType role, Guid? operatorId = null, CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
        {
            throw new AccessControlDomainException("用户标识不可为空", "USER_ROLE_USER_EMPTY");
        }

        if (!Enum.IsDefined(role))
        {
            throw new AccessControlDomainException("未定义的角色类型", "USER_ROLE_ROLE_INVALID");
        }

        // 幂等：已存在生效分配则忽略
        var existing = await _userRoleRepository.GetActiveAssignmentAsync(userId, role, ct);
        if (existing is not null)
        {
            return;
        }

        var assignment = UserRoleAssignment.Create(Guid.NewGuid(), userId, role, operatorId);
        await _userRoleRepository.AddAsync(assignment, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    /// <inheritdoc />
    public async Task RevokeRoleAsync(Guid userId, RoleType role, Guid? operatorId = null, CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
        {
            throw new AccessControlDomainException("用户标识不可为空", "USER_ROLE_USER_EMPTY");
        }

        if (!Enum.IsDefined(role))
        {
            throw new AccessControlDomainException("未定义的角色类型", "USER_ROLE_ROLE_INVALID");
        }

        // INV-13：禁止管理员撤销自身 Admin 角色
        if (operatorId.HasValue && operatorId.Value == userId && role == RoleType.Admin)
        {
            throw new AccessControlDomainException("禁止撤销自身管理员角色", "USER_REVOKE_ADMIN_SELF");
        }

        var assignment = await _userRoleRepository.GetActiveAssignmentAsync(userId, role, ct);
        if (assignment is null)
        {
            // 幂等：未分配则忽略
            return;
        }

        // INV-12：至少保留一个角色
        var activeCount = await _userRoleRepository.CountActiveRolesAsync(userId, ct);
        if (activeCount <= 1)
        {
            throw new AccessControlDomainException("至少保留一个角色", "USER_LAST_ROLE");
        }

        assignment.Revoke(operatorId);
        await _userRoleRepository.UpdateAsync(assignment, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    /// <inheritdoc />
    public async Task AssignDefaultRoleOnRegisterAsync(Guid userId, CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
        {
            throw new AccessControlDomainException("用户标识不可为空", "USER_ROLE_USER_EMPTY");
        }

        // 注册即授予 Buyer 角色（自助注册场景，operatorId 为 null）
        await AssignRoleAsync(userId, RoleType.Buyer, operatorId: null, ct);
    }
}
