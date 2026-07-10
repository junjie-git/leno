using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Domain.ValueObjects;

namespace Leno.SystemAdmin.Application;

/// <summary>
/// 运营人员管理应用服务接口。
/// </summary>
public interface IOperatorAppService
{
    /// <summary>创建运营人员。</summary>
    Task<OperatorDto> CreateAsync(SaveOperatorDto dto, CancellationToken ct = default);

    /// <summary>更新运营人员权限（合并新增权限码）。</summary>
    Task<OperatorDto> UpdatePermissionsAsync(Guid operatorId, AssignPermissionsDto dto, CancellationToken ct = default);

    /// <summary>启用运营人员。</summary>
    Task ActivateAsync(Guid operatorId, CancellationToken ct = default);

    /// <summary>停用运营人员。</summary>
    Task DeactivateAsync(Guid operatorId, CancellationToken ct = default);

    /// <summary>按标识获取运营人员。</summary>
    Task<OperatorDto?> GetByIdAsync(Guid operatorId, CancellationToken ct = default);

    /// <summary>分页查询运营人员，支持角色与状态过滤。</summary>
    Task<OperatorListResultDto> QueryAsync(OperatorRole? role, OperatorStatus? status, int page, int pageSize, CancellationToken ct = default);
}
