using System.Text;
using System.Text.Json;
using Leno.SystemAdmin.Domain.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Leno.SystemAdmin.Infrastructure.Services;

/// <summary>
/// Elasticsearch 索引重建触发器实现，通过 Elasticsearch REST API 执行 reindex 操作并查询进度。
/// 配置节：<c>Elasticsearch:Nodes</c>（数组），或 <c>Elasticsearch:Url</c>（单节点）。
/// </summary>
public sealed class ElasticsearchRebuildTrigger : IIndexRebuildTrigger
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ElasticsearchRebuildTrigger> _logger;

    private const string EsNodesConfigKey = "Elasticsearch:Nodes";
    private const string EsUrlConfigKey = "Elasticsearch:Url";

    public ElasticsearchRebuildTrigger(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<ElasticsearchRebuildTrigger> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;

        _httpClient.Timeout = TimeSpan.FromMinutes(10);
    }

    /// <inheritdoc />
    public async Task StartAsync(Guid taskId, string targetContext, string indexName, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(targetContext);
        ArgumentNullException.ThrowIfNull(indexName);

        var baseUrl = GetElasticsearchBaseUrl();
        var sourceIndex = $"{targetContext.ToLowerInvariant()}_{indexName}";
        var destIndex = $"{sourceIndex}_reindex_{taskId:N}";

        _logger.LogInformation(
            "启动 ES 索引重建：TaskId={TaskId}, Source={SourceIndex}, Dest={DestIndex}",
            taskId, sourceIndex, destIndex);

        // 1. 创建目标索引（带映射）
        await CreateTargetIndexAsync(baseUrl, sourceIndex, destIndex, ct);

        // 2. 提交 reindex 任务
        var reindexUrl = $"{baseUrl}/_reindex?wait_for_completion=false";
        var reindexBody = new
        {
            source = new { index = sourceIndex },
            dest = new { index = destIndex }
        };

        var json = JsonSerializer.Serialize(reindexBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(reindexUrl, content, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError(
                "ES reindex 启动失败：TaskId={TaskId}, StatusCode={StatusCode}, Error={Error}",
                taskId, (int)response.StatusCode, errorBody);
            throw new InvalidOperationException($"ES reindex 启动失败：HTTP {(int)response.StatusCode}，{errorBody}");
        }

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(responseJson);

        if (doc.RootElement.TryGetProperty("task", out var taskNode))
        {
            var esTaskId = taskNode.GetString();
            _logger.LogInformation(
                "ES reindex 任务已提交：TaskId={TaskId}, EsTaskId={EsTaskId}",
                taskId, esTaskId);
        }
    }

    /// <inheritdoc />
    public async Task<int> GetProgressAsync(Guid taskId, CancellationToken ct)
    {
        var baseUrl = GetElasticsearchBaseUrl();

        // 查询所有运行中的 reindex 任务
        var tasksUrl = $"{baseUrl}/_tasks?actions=*reindex&detailed=true";

        try
        {
            var response = await _httpClient.GetAsync(tasksUrl, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("ES _tasks API 查询失败：StatusCode={StatusCode}", (int)response.StatusCode);
                return 0;
            }

            var responseJson = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(responseJson);

            if (!doc.RootElement.TryGetProperty("nodes", out var nodes))
            {
                return 0;
            }

            // 遍历所有节点查找匹配的 reindex 任务
            foreach (var nodeProp in nodes.EnumerateObject())
            {
                if (!nodeProp.Value.TryGetProperty("tasks", out var tasks))
                {
                    continue;
                }

                foreach (var taskProp in tasks.EnumerateObject())
                {
                    var task = taskProp.Value;
                    if (!task.TryGetProperty("status", out var status))
                    {
                        continue;
                    }

                    // 检查是否包含我们的 reindex 目标索引
                    if (status.TryGetProperty("created", out var created) &&
                        status.TryGetProperty("total", out var total) &&
                        total.GetInt64() > 0)
                    {
                        var createdCount = created.GetInt64();
                        var totalCount = total.GetInt64();
                        var progress = (int)(createdCount * 100 / totalCount);
                        return Math.Min(progress, 100);
                    }
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查询 ES reindex 进度失败：TaskId={TaskId}", taskId);
            return 0;
        }
    }

    private async Task CreateTargetIndexAsync(string baseUrl, string sourceIndex, string destIndex, CancellationToken ct)
    {
        // 获取源索引映射
        var mappingUrl = $"{baseUrl}/{sourceIndex}/_mapping";
        string? mappingJson = null;

        try
        {
            var mappingResponse = await _httpClient.GetAsync(mappingUrl, ct);
            if (mappingResponse.IsSuccessStatusCode)
            {
                mappingJson = await mappingResponse.Content.ReadAsStringAsync(ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "获取源索引映射失败，将使用默认映射：Index={SourceIndex}", sourceIndex);
        }

        // 创建目标索引
        var createUrl = $"{baseUrl}/{destIndex}";
        StringContent content;

        if (!string.IsNullOrWhiteSpace(mappingJson))
        {
            // 从 mapping 响应中提取 mappings 部分
            try
            {
                using var doc = JsonDocument.Parse(mappingJson);
                if (doc.RootElement.TryGetProperty(sourceIndex, out var sourceIndexNode) &&
                    sourceIndexNode.TryGetProperty("mappings", out var mappingsNode))
                {
                    var createBody = new Dictionary<string, object>
                    {
                        ["mappings"] = JsonSerializer.Deserialize<JsonElement>(mappingsNode.GetRawText())
                    };
                    mappingJson = JsonSerializer.Serialize(createBody);
                }
            }
            catch
            {
                mappingJson = null;
            }
        }

        content = new StringContent(
            mappingJson ?? "{}",
            Encoding.UTF8,
            "application/json");

        var createResponse = await _httpClient.PutAsync(createUrl, content, ct);

        if (!createResponse.IsSuccessStatusCode)
        {
            var errorBody = await createResponse.Content.ReadAsStringAsync(ct);
            _logger.LogWarning(
                "创建目标索引失败：DestIndex={DestIndex}, StatusCode={StatusCode}, Error={Error}",
                destIndex, (int)createResponse.StatusCode, errorBody);
        }
        else
        {
            _logger.LogInformation("目标索引已创建：DestIndex={DestIndex}", destIndex);
        }
    }

    private string GetElasticsearchBaseUrl()
    {
        var url = _configuration[EsUrlConfigKey];
        if (!string.IsNullOrWhiteSpace(url))
        {
            return url.TrimEnd('/');
        }

        var nodesSection = _configuration.GetSection(EsNodesConfigKey);
        if (nodesSection.Exists())
        {
            var firstNode = nodesSection.GetChildren().FirstOrDefault()?.Value;
            if (!string.IsNullOrWhiteSpace(firstNode))
            {
                return firstNode.TrimEnd('/');
            }
        }

        return "http://localhost:9200";
    }
}