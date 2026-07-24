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

    /// <summary>
    /// 搜索文档（带请求配置回调，支持排序等扩展）。
    /// <paramref name="query"/> 为 null 时匹配全部；<paramref name="configure"/> 为 null 时不附加额外配置（等价于无回调重载）。
    /// </summary>
    /// <param name="configure">搜索请求配置回调，可在搜索描述符上追加排序、聚合等。可为 null。</param>
    Task<(IReadOnlyList<T> Items, long Total)> SearchAsync(
        string indexName,
        Func<QueryDescriptor<T>, Query>? query,
        Action<SearchRequestDescriptor<T>>? configure,
        int from,
        int size,
        CancellationToken ct = default);

    /// <summary>按 Id 删除文档。</summary>
    Task<bool> DeleteByIdAsync(string id, string indexName, CancellationToken ct = default);
}
