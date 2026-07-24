using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Microsoft.Extensions.Logging;

namespace Leno.Infrastructure.ReadModel;

/// <summary>
/// 基于 <see cref="ElasticsearchClient"/> 的读模型仓储实现，提供索引 CRUD 与搜索。
/// </summary>
/// <typeparam name="T">读模型文档类型。</typeparam>
public sealed class EsReadModelRepository<T> : IEsReadModelRepository<T>
    where T : class
{
    private readonly ElasticsearchClient _client;
    private readonly ILogger<EsReadModelRepository<T>> _logger;

    public EsReadModelRepository(ElasticsearchClient client, ILogger<EsReadModelRepository<T>> logger)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(logger);
        _client = client;
        _logger = logger;
    }

    public async Task<bool> IndexAsync(T document, string id, string indexName, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrEmpty(id);
        ArgumentException.ThrowIfNullOrEmpty(indexName);

        var response = await _client.IndexAsync(document, idx => idx.Index(indexName).Id(id), ct);
        if (!response.IsValidResponse)
        {
            _logger.LogError("ES 索引失败 Index={Index} Id={Id} Error={Error}",
                indexName, id, response.DebugInformation);
            return false;
        }

        return true;
    }

    public async Task<T?> GetByIdAsync(string id, string indexName, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        ArgumentException.ThrowIfNullOrEmpty(indexName);

        var response = await _client.GetAsync<T>(id, g => g.Index(indexName), ct);
        if (!response.IsValidResponse || !response.Found)
        {
            return null;
        }

        return response.Source;
    }

    public async Task<(IReadOnlyList<T> Items, long Total)> SearchAsync(
        string indexName,
        Func<QueryDescriptor<T>, Query>? query,
        int from,
        int size,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(indexName);

        var response = await _client.SearchAsync<T>(s =>
        {
            s.Index(indexName).From(from).Size(size);
            if (query is not null)
            {
                var q = query(new QueryDescriptor<T>());
                s.Query(q);
            }
        }, ct);

        if (!response.IsValidResponse)
        {
            _logger.LogError("ES 搜索失败 Index={Index} Error={Error}", indexName, response.DebugInformation);
            return (Array.Empty<T>(), 0);
        }

        return (response.Documents.ToList(), response.Total);
    }

    /// <summary>
    /// 搜索文档（带请求配置回调，支持排序等扩展）。
    /// <paramref name="configure"/> 为 null 时等价于无回调重载；非 null 时在查询构建后追加配置（如排序）。
    /// </summary>
    public async Task<(IReadOnlyList<T> Items, long Total)> SearchAsync(
        string indexName,
        Func<QueryDescriptor<T>, Query>? query,
        Action<SearchRequestDescriptor<T>>? configure,
        int from,
        int size,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(indexName);

        var response = await _client.SearchAsync<T>(s =>
        {
            s.Index(indexName).From(from).Size(size);
            if (query is not null)
            {
                var q = query(new QueryDescriptor<T>());
                s.Query(q);
            }
            configure?.Invoke(s);
        }, ct);

        if (!response.IsValidResponse)
        {
            _logger.LogError("ES 搜索失败 Index={Index} Error={Error}", indexName, response.DebugInformation);
            return (Array.Empty<T>(), 0);
        }

        return (response.Documents.ToList(), response.Total);
    }

    public async Task<bool> DeleteByIdAsync(string id, string indexName, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        ArgumentException.ThrowIfNullOrEmpty(indexName);

        var response = await _client.DeleteAsync<T>(id, d => d.Index(indexName), ct);
        if (!response.IsValidResponse)
        {
            _logger.LogError("ES 删除失败 Index={Index} Id={Id} Error={Error}",
                indexName, id, response.DebugInformation);
            return false;
        }

        return true;
    }
}
