using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;

namespace Leno.SystemAdmin.Application.Services;

/// <summary>
/// 运营人员管理应用服务实现。
/// </summary>
public sealed class OperatorAppService : IOperatorAppService
{
    private readonly IOperatorRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<OperatorAppService> _logger;

    public OperatorAppService(
        IOperatorRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<OperatorAppService> logger)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(logger);
        _repository = repository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<OperatorDto> CreateAsync(SaveOperatorDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var operatorId = Guid.NewGuid();
        var entity = Operator.Create(operatorId, dto.UserId, dto.DisplayName, dto.Role, dto.Permissions);

        await _repository.AddAsync(entity, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        _logger.LogInformation("运营人员已创建：{OperatorId}", operatorId);
        return ToDto(entity);
    }

    /// <inheritdoc />
    public async Task<OperatorDto> UpdatePermissionsAsync(Guid operatorId, AssignPermissionsDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var entity = await RequireOperatorAsync(operatorId, ct);
        entity.AssignPermissions(dto.Permissions);

        await _repository.UpdateAsync(entity, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        _logger.LogInformation("运营人员权限已更新：{OperatorId}", operatorId);
        return ToDto(entity);
    }

    /// <inheritdoc />
    public async Task ActivateAsync(Guid operatorId, CancellationToken ct = default)
    {
        var entity = await RequireOperatorAsync(operatorId, ct);
        entity.Activate();

        await _repository.UpdateAsync(entity, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        _logger.LogInformation("运营人员已启用：{OperatorId}", operatorId);
    }

    /// <inheritdoc />
    public async Task DeactivateAsync(Guid operatorId, CancellationToken ct = default)
    {
        var entity = await RequireOperatorAsync(operatorId, ct);
        entity.Deactivate();

        await _repository.UpdateAsync(entity, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        _logger.LogInformation("运营人员已停用：{OperatorId}", operatorId);
    }

    /// <inheritdoc />
    public async Task<OperatorDto?> GetByIdAsync(Guid operatorId, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(operatorId, ct);
        return entity is null ? null : ToDto(entity);
    }

    /// <inheritdoc />
    public async Task<OperatorListResultDto> QueryAsync(OperatorRole? role, OperatorStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        var items = await _repository.QueryAsync(role, status, page, pageSize, ct);
        var total = await _repository.CountAsync(role, status, ct);

        return new OperatorListResultDto
        {
            Items = items.Select(ToDto).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    private async Task<Operator> RequireOperatorAsync(Guid operatorId, CancellationToken ct)
        => await _repository.GetByIdAsync(operatorId, ct)
           ?? throw new InvalidOperationException($"运营人员 {operatorId} 不存在");

    private static OperatorDto ToDto(Operator entity)
        => new()
        {
            OperatorId = entity.OperatorId,
            UserId = entity.UserId,
            DisplayName = entity.DisplayName,
            Role = entity.Role,
            Permissions = new List<string>(entity.Permissions),
            Status = entity.Status,
            LastLoginAt = entity.LastLoginAt,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
}
