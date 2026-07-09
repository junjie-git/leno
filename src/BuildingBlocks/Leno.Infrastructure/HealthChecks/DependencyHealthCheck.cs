using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Leno.Infrastructure.HealthChecks;

/// <summary>
/// Redis 依赖健康检查，验证 Redis 连接可用性。
/// 通过 <see cref="IConnectionMultiplexer"/> 执行 PING，失败标记 Unhealthy。
/// 注册：<c>AddCheck&lt;RedisHealthCheck&gt;("redis", tags: new[] { "ready" })</c>。
/// </summary>
public sealed class RedisHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisHealthCheck> _logger;

    public RedisHealthCheck(IConnectionMultiplexer redis, ILogger<RedisHealthCheck> logger)
    {
        ArgumentNullException.ThrowIfNull(redis);
        ArgumentNullException.ThrowIfNull(logger);
        _redis = redis;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var database = _redis.GetDatabase();
            var pong = await database.PingAsync();
            return HealthCheckResult.Healthy($"Redis PING 正常，耗时 {pong.TotalMilliseconds:F1}ms");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis 健康检查失败");
            return HealthCheckResult.Unhealthy("Redis 不可用", ex);
        }
    }
}

/// <summary>
/// Elasticsearch 依赖健康检查，验证 ES 集群可达。
/// 通过 ping 集群根路径返回的状态判断。
/// </summary>
public sealed class ElasticsearchHealthCheck : IHealthCheck
{
    private readonly Elastic.Clients.Elasticsearch.ElasticsearchClient _client;
    private readonly ILogger<ElasticsearchHealthCheck> _logger;

    public ElasticsearchHealthCheck(
        Elastic.Clients.Elasticsearch.ElasticsearchClient client,
        ILogger<ElasticsearchHealthCheck> logger)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(logger);
        _client = client;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var response = await _client.PingAsync(cancellationToken);
            return response.IsValidResponse
                ? HealthCheckResult.Healthy("Elasticsearch 可达")
                : HealthCheckResult.Unhealthy("Elasticsearch Ping 返回无效响应");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Elasticsearch 健康检查失败");
            return HealthCheckResult.Unhealthy("Elasticsearch 不可用", ex);
        }
    }
}
