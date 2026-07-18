using System.Text.Json;
using Leno.UserAuth.Application.DTOs;
using Leno.UserAuth.Application.Exceptions;
using Leno.UserAuth.Domain.Aggregates;
using Leno.UserAuth.Domain.Exceptions;
using Leno.UserAuth.Domain.Repositories;
using Leno.UserAuth.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.UserAuth.Application.Services;

/// <summary>
/// 用户管理后台应用服务实现，编排用户分页查询、角色分配与账户状态管理。
/// 写操作在事务内写入审计日志（<see cref="AuditLog"/>），技术上下文（IP/UA/TraceId）由审计拦截器填充。
/// </summary>
public sealed class UserAdminAppService : IUserAdminAppService
{
    private readonly IUserRepository _userRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UserAdminAppService(
        IUserRepository userRepository,
        IAuditLogRepository auditLogRepository,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _auditLogRepository = auditLogRepository;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task<PagedResult<AdminUserDto>> QueryUsersAsync(AdminUserQueryDto query, CancellationToken ct = default)
    {
        var (items, total) = await _userRepository.QueryAsync(
            query.Keyword,
            query.Role,
            query.Status,
            NormalizePage(query.Page),
            NormalizePageSize(query.PageSize),
            ct);

        return PagedResult.Create(
            items.Select(ToAdminUserDto).ToList(),
            total,
            NormalizePage(query.Page),
            NormalizePageSize(query.PageSize));
    }

    /// <inheritdoc />
    public async Task<AdminUserDto> GetUserAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await RequireUserAsync(userId, ct);
        return ToAdminUserDto(user);
    }

    /// <inheritdoc />
    public async Task AssignRolesAsync(Guid targetUserId, AssignRolesDto dto, Guid operatorId, CancellationToken ct = default)
    {
        if (dto.Roles is null || dto.Roles.Count == 0)
        {
            throw new UserAuthValidationException("待分配角色不可为空");
        }

        var user = await RequireUserAsync(targetUserId, ct);
        var before = Snapshot(user);

        foreach (var code in dto.Roles)
        {
            if (!Enum.TryParse<RoleType>(code, ignoreCase: true, out var role))
            {
                throw new UserAuthValidationException($"未知角色编码：{code}");
            }

            user.AssignRole(role, operatorId);
        }

        var after = Snapshot(user);
        await _userRepository.UpdateAsync(user, ct);
        await WriteAuditAsync(operatorId, "RoleAssign", targetUserId, before, after, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    /// <inheritdoc />
    public async Task SuspendAsync(Guid targetUserId, SuspendUserDto dto, Guid operatorId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Reason))
        {
            throw new UserAuthValidationException("锁定原因不可为空");
        }

        if (dto.DurationMinutes is <= 0 or > 1440)
        {
            throw new UserAuthValidationException("锁定时长须为 1-1440 分钟");
        }

        var user = await RequireUserAsync(targetUserId, ct);
        var before = Snapshot(user);

        user.Lock(dto.Reason, TimeSpan.FromMinutes(dto.DurationMinutes));

        var after = Snapshot(user);
        await _userRepository.UpdateAsync(user, ct);
        await WriteAuditAsync(operatorId, "UserSuspend", targetUserId, before, after, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    /// <inheritdoc />
    public async Task ResumeAsync(Guid targetUserId, Guid operatorId, CancellationToken ct = default)
    {
        var user = await RequireUserAsync(targetUserId, ct);
        var before = Snapshot(user);

        if (user.Status == AccountStatus.Locked)
        {
            user.Unlock();
        }
        else if (user.Status == AccountStatus.Disabled)
        {
            user.Activate();
        }
        else
        {
            throw new UserAuthDomainException("仅锁定或禁用状态的账户可恢复", "USER_NOT_SUSPENDED");
        }

        var after = Snapshot(user);
        await _userRepository.UpdateAsync(user, ct);
        await WriteAuditAsync(operatorId, "UserResume", targetUserId, before, after, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    private async Task WriteAuditAsync(
        Guid operatorId,
        string action,
        Guid targetUserId,
        string? beforeSnapshot,
        string? afterSnapshot,
        CancellationToken ct)
    {
        var auditLog = AuditLog.Create(
            Guid.NewGuid(),
            operatorId,
            action,
            "User",
            targetUserId.ToString(),
            beforeSnapshot,
            afterSnapshot);

        await _auditLogRepository.AddAsync(auditLog, ct);
    }

    private async Task<User> RequireUserAsync(Guid userId, CancellationToken ct)
    {
        var user = await _userRepository.GetByIdAsync(userId, ct);
        if (user is null)
        {
            throw new UserAuthDomainException("用户不存在", "USER_NOT_FOUND");
        }

        return user;
    }

    private static string Snapshot(User user)
        => JsonSerializer.Serialize(new
        {
            user.Id,
            user.Username,
            user.Status,
            Roles = user.Roles.Select(r => r.Code).ToArray()
        });

    private static int NormalizePage(int page) => page < 1 ? 1 : page;

    private static int NormalizePageSize(int pageSize)
        => pageSize is <= 0 or > 100 ? 20 : pageSize;

    private static AdminUserDto ToAdminUserDto(User user)
        => new()
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            Nickname = user.Nickname,
            Status = user.Status,
            Roles = user.Roles.Select(r => r.Code).ToList(),
            FailedLoginCount = user.FailedLoginCount,
            LockedUntil = user.LockedUntil,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
}
