using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;

namespace Leno.Infrastructure.ReadModel;

/// <summary>
/// Elasticsearch 读模型仓储抽象，提供索引 CRUD 与搜索。
/// 基于 CQRS 读库同步：写库变更通过集成事件触发读模型索引。
/// </summary>
/// <typeparam name="T">读模型文档类型。</typeparam>
public interface IEsReadModelRepository<T> where T : class
{
    /// <summary>索引（新增/更新）单个文档。</summary>
    Task<bool> IndexAsync(T document, string id, string indexName, CancellationToken ct = default);

    /// <summary>按 Id 获取文档，不存在返回 null。</summary>
    Task<T?> GetByIdAsync(string id, string indexName, CancellationToken ct = default);

    /// <summary>
    /// 搜索文档。<paramref name="query"/> 为 null 时匹配全部。
    /// </summary>
    Task<(IReadOnlyList<T> Items, long Total)> SearchAsync(
        string indexName,
        Func<QueryDescriptor<T>, Query>? query,
        int from,
        int size,
        CancellationToken ct = default);

    /// <summary>按 Id 删除文档。</summary>
    Task<bool> DeleteByIdAsync(string id, string indexName, CancellationToken ct = default);
}
