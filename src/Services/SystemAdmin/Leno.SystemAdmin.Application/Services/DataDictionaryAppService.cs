using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;

namespace Leno.SystemAdmin.Application.Services;

/// <summary>
/// 数据字典管理应用服务实现。
/// </summary>
public sealed class DataDictionaryAppService : IDataDictionaryAppService
{
    private readonly IDataDictionaryRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DataDictionaryAppService> _logger;

    public DataDictionaryAppService(
        IDataDictionaryRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<DataDictionaryAppService> logger)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(logger);
        _repository = repository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<DataDictionaryDto> CreateAsync(SaveDataDictionaryDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var dictionaryId = Guid.NewGuid();
        var entity = DataDictionary.Create(dictionaryId, dto.Code, dto.Name, dto.Description);

        await _repository.AddAsync(entity, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        _logger.LogInformation("数据字典已创建：{DictionaryId}（Code={Code}）", dictionaryId, entity.Code);
        return ToDto(entity);
    }

    /// <inheritdoc />
    public async Task<DataDictionaryDto> UpdateAsync(Guid dictionaryId, SaveDataDictionaryDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var entity = await RequireDictionaryAsync(dictionaryId, ct);
        entity.Update(dto.Name, dto.Description);

        await _repository.UpdateAsync(entity, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        _logger.LogInformation("数据字典已更新：{DictionaryId}", dictionaryId);
        return ToDto(entity);
    }

    /// <inheritdoc />
    public async Task EnableAsync(Guid dictionaryId, CancellationToken ct = default)
    {
        var entity = await RequireDictionaryAsync(dictionaryId, ct);
        entity.Enable();

        await _repository.UpdateAsync(entity, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        _logger.LogInformation("数据字典已启用：{DictionaryId}", dictionaryId);
    }

    /// <inheritdoc />
    public async Task DisableAsync(Guid dictionaryId, CancellationToken ct = default)
    {
        var entity = await RequireDictionaryAsync(dictionaryId, ct);
        entity.Disable();

        await _repository.UpdateAsync(entity, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        _logger.LogInformation("数据字典已停用：{DictionaryId}", dictionaryId);
    }

    /// <inheritdoc />
    public async Task<DataDictionaryDto?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        var entity = await _repository.GetByCodeAsync(code, ct);
        return entity is null ? null : ToDto(entity);
    }

    /// <inheritdoc />
    public async Task<DataDictionaryDto?> GetByIdAsync(Guid dictionaryId, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(dictionaryId, ct);
        return entity is null ? null : ToDto(entity);
    }

    /// <inheritdoc />
    public async Task<DataDictionaryListResultDto> QueryAsync(string? name, DictionaryStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        var items = await _repository.QueryAsync(name, status, page, pageSize, ct);
        var total = await _repository.CountAsync(name, status, ct);

        return new DataDictionaryListResultDto
        {
            Items = items.Select(ToDto).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <inheritdoc />
    public async Task<DataDictionaryDto> AddItemAsync(Guid dictionaryId, AddDictionaryItemDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var entity = await RequireDictionaryAsync(dictionaryId, ct);
        entity.AddItem(Guid.NewGuid(), dto.Code, dto.Label, dto.Value, dto.SortOrder);

        await _repository.UpdateAsync(entity, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        _logger.LogInformation("字典项已新增：DictionaryId={DictionaryId}", dictionaryId);
        return ToDto(entity);
    }

    /// <inheritdoc />
    public async Task<DataDictionaryDto> UpdateItemAsync(Guid dictionaryId, Guid itemId, UpdateDictionaryItemDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var entity = await RequireDictionaryAsync(dictionaryId, ct);
        entity.UpdateItem(itemId, dto.Label, dto.Value, dto.SortOrder);

        await _repository.UpdateAsync(entity, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        _logger.LogInformation("字典项已更新：DictionaryId={DictionaryId}，ItemId={ItemId}", dictionaryId, itemId);
        return ToDto(entity);
    }

    /// <inheritdoc />
    public async Task RemoveItemAsync(Guid dictionaryId, Guid itemId, CancellationToken ct = default)
    {
        var entity = await RequireDictionaryAsync(dictionaryId, ct);
        entity.RemoveItem(itemId);

        await _repository.UpdateAsync(entity, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        _logger.LogInformation("字典项已移除：DictionaryId={DictionaryId}，ItemId={ItemId}", dictionaryId, itemId);
    }

    private async Task<DataDictionary> RequireDictionaryAsync(Guid dictionaryId, CancellationToken ct)
        => await _repository.GetByIdAsync(dictionaryId, ct)
           ?? throw new InvalidOperationException($"数据字典 {dictionaryId} 不存在");

    private static DataDictionaryDto ToDto(DataDictionary entity)
        => new()
        {
            DictionaryId = entity.DictionaryId,
            Code = entity.Code,
            Name = entity.Name,
            Description = entity.Description,
            Status = entity.Status,
            Items = entity.Items.Select(ToItemDto).ToList(),
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };

    private static DictionaryItemDto ToItemDto(DictionaryItem item)
        => new()
        {
            ItemId = item.Id,
            DictionaryId = item.DictionaryId,
            Code = item.Code,
            Label = item.Label,
            Value = item.Value,
            SortOrder = item.SortOrder,
            Status = item.Status
        };
}
