using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using Testcontainers.Elasticsearch;
using Testcontainers.MsSql;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;

namespace Leno.Testing.Fixtures;

public sealed class ContainerFixture : IAsyncLifetime
{
    private const string SqlPassword = "Leno@Test123!";
    private const int SqlPort = 1433;
    private const int RedisPort = 6379;
    private const int RabbitMqPort = 5672;
    private const int RabbitMqManagementPort = 15672;
    private const int ElasticsearchPort = 9200;

    public MsSqlContainer SqlServer { get; private set; } = null!;
    public RedisContainer Redis { get; private set; } = null!;
    public RabbitMqContainer RabbitMq { get; private set; } = null!;
    public ElasticsearchContainer Elasticsearch { get; private set; } = null!;

    public string SqlConnectionString => SqlServer.GetConnectionString();
    public string RedisConnectionString => Redis.GetConnectionString();
    public string RabbitMqConnectionString => $"amqp://guest:guest@{RabbitMq.Hostname}:{RabbitMq.GetMappedPublicPort(RabbitMqPort)}";
    public string ElasticsearchUrl => $"http://{Elasticsearch.Hostname}:{Elasticsearch.GetMappedPublicPort(ElasticsearchPort)}";

    public async Task InitializeAsync()
    {
        // 固定 MsSql 大版本为 2019：与 docker-compose.yml、CI 迁移校验 Job（mssql 2019 容器）
        // 一致，避免测试库与部署环境跨版本漂移；MsSqlBuilder 默认 2022-latest 不保证与生产一致
        SqlServer = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2019-latest")
            .WithPassword(SqlPassword)
            .WithPortBinding(SqlPort, true)
            .WithWaitStrategy(Wait.ForWindowsContainer()
                .UntilCommandIsCompleted("/opt/mssql-tools18/bin/sqlcmd", "-S", "localhost", "-U", "sa", "-P", SqlPassword, "-Q", "SELECT 1", "-C"))
            .Build();

        Redis = new RedisBuilder()
            .WithPortBinding(RedisPort, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilCommandIsCompleted("redis-cli", "ping"))
            .Build();

        RabbitMq = new RabbitMqBuilder()
            .WithPortBinding(RabbitMqPort, true)
            .WithPortBinding(RabbitMqManagementPort, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(RabbitMqPort))
            .Build();

        // 与 docker-compose.yml 对齐：显式禁用 xpack security + 单节点。
        // ES 8.x 默认对 9200 启用 HTTPS，明文 HTTP 等待探针将永远失败，
        // 导致 InitializeAsync 无限挂起（2026-09-28 实测：6 个集成测试挂起 24 分钟无结果）
        Elasticsearch = new ElasticsearchBuilder()
            .WithImage("docker.elastic.co/elasticsearch/elasticsearch:8.13.0")
            .WithEnvironment("xpack.security.enabled", "false")
            .WithEnvironment("discovery.type", "single-node")
            .WithPortBinding(ElasticsearchPort, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(ElasticsearchPort)))
            .Build();

        await Task.WhenAll(
            SqlServer.StartAsync(),
            Redis.StartAsync(),
            RabbitMq.StartAsync(),
            Elasticsearch.StartAsync()
        );
    }

    public async Task DisposeAsync()
    {
        var tasks = new List<Task>();
        if (SqlServer is not null) tasks.Add(SqlServer.DisposeAsync().AsTask());
        if (Redis is not null) tasks.Add(Redis.DisposeAsync().AsTask());
        if (RabbitMq is not null) tasks.Add(RabbitMq.DisposeAsync().AsTask());
        if (Elasticsearch is not null) tasks.Add(Elasticsearch.DisposeAsync().AsTask());
        await Task.WhenAll(tasks);
    }
}