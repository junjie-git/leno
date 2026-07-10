using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Domain.ValueObjects;

namespace Leno.SystemAdmin.Application;

/// <summary>
/// 数据字典管理应用服务接口。
/// </summary>
public interface IDataDictionaryAppService
{
    /// <summary>创建数据字典。</summary>
    Task<DataDictionaryDto> CreateAsync(SaveDataDictionaryDto dto, CancellationToken ct = default);

    /// <summary>更新数据字典（编码不可变）。</summary>
    Task<DataDictionaryDto> UpdateAsync(Guid dictionaryId, SaveDataDictionaryDto dto, CancellationToken ct = default);

    /// <summary>启用字典。</summary>
    Task EnableAsync(Guid dictionaryId, CancellationToken ct = default);

    /// <summary>停用字典。</summary>
    Task DisableAsync(Guid dictionaryId, CancellationToken ct = default);

    /// <summary>按编码获取字典（含字典项）。</summary>
    Task<DataDictionaryDto?> GetByCodeAsync(string code, CancellationToken ct = default);

    /// <summary>按标识获取字典（含字典项）。</summary>
    Task<DataDictionaryDto?> GetByIdAsync(Guid dictionaryId, CancellationToken ct = default);

    /// <summary>分页查询字典，支持名称与状态过滤。</summary>
    Task<DataDictionaryListResultDto> QueryAsync(string? name, DictionaryStatus? status, int page, int pageSize, CancellationToken ct = default);

    /// <summary>新增字典项。</summary>
    Task<DataDictionaryDto> AddItemAsync(Guid dictionaryId, AddDictionaryItemDto dto, CancellationToken ct = default);

    /// <summary>更新字典项。</summary>
    Task<DataDictionaryDto> UpdateItemAsync(Guid dictionaryId, Guid itemId, UpdateDictionaryItemDto dto, CancellationToken ct = default);

    /// <summary>移除字典项（幂等）。</summary>
    Task RemoveItemAsync(Guid dictionaryId, Guid itemId, CancellationToken ct = default);
}
