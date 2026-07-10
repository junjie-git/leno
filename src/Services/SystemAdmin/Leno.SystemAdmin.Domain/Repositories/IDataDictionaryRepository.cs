using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.SystemAdmin.Domain.Repositories;

/// <summary>
/// 数据字典仓储接口，定义在领域层，由基础设施层实现。
/// 支持按编码、名称、状态查询，写操作由工作单元统一提交。
/// </summary>
public interface IDataDictionaryRepository : IRepository<DataDictionary>
{
    /// <summary>
    /// 按编码获取字典（含字典项）。
    /// </summary>
    /// <param name="code">字典编码。</param>
    /// <param name="ct">取消令牌。</param>
    Task<DataDictionary?> GetByCodeAsync(string code, CancellationToken ct = default);

    /// <summary>
    /// 分页查询字典，支持名称与状态过滤。
    /// </summary>
    /// <param name="name">名称关键词，可空。</param>
    /// <param name="status">状态过滤，可空表示不限。</param>
    /// <param name="page">页码，从 1 起。</param>
    /// <param name="pageSize">每页条数。</param>
    /// <param name="ct">取消令牌。</param>
    Task<List<DataDictionary>> QueryAsync(string? name, DictionaryStatus? status, int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// 统计字典数量，支持名称与状态过滤。
    /// </summary>
    /// <param name="name">名称关键词，可空。</param>
    /// <param name="status">状态过滤，可空表示不限。</param>
    /// <param name="ct">取消令牌。</param>
    Task<int> CountAsync(string? name, DictionaryStatus? status, CancellationToken ct = default);
}
