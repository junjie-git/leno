using System.Net.Http.Headers;
using System.Text;
using Leno.SharedKernel.Abstractions;
using Leno.SystemAdmin.Domain.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Leno.SystemAdmin.Infrastructure.Jobs;

/// <summary>
/// DLQ（死信队列）清理作业，由 Quartz 定时触发（默认每小时一次）。
/// <para>
/// 工作原理：<see cref="RabbitMqDeadLetterManager"/> 采用 <c>ack_requeue_true</c> 拉取策略，
/// 消息拉取后回队不入库删除，导致 DLQ 消息无限堆积。本作业定期清理 DLQ 中的消息，
/// 清理前先确认本地 <c>DeadLetterMessages</c> 表已有入库副本（Count > 0），避免清理未入库消息。
/// </para>
/// <para>
/// 清理方式：调用 RabbitMQ Management API <c>DELETE /api/queues/{vhost}/{queue}/contents</c> 清空指定 DLQ 队列。
/// 配置节：<c>RabbitMQ:ManagementApi</c>（Host、Username、Password、VHost）与 <c>DlqCleanup:QueueNames</c>（逗号分隔，默认 dead-letter-queue）。
/// </para>
/// </summary>
public sealed class DlqCleanupJob : IJob
{
    private readonly IServiceProvider _serviceProvider;

    private const string ManagementApiConfigKey = "RabbitMQ:ManagementApi";
    private const string QueueNamesConfigKey = "DlqCleanup:QueueNames";
    private const string DefaultQueueName = "dead-letter-queue";

    public DlqCleanupJob(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public async Task Execute(IJobExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var ct = context.CancellationToken;

        // ILogger<T> 为单例，从根容器解析即可
        var logger = _serviceProvider.GetRequiredService<ILogger<DlqCleanupJob>>();

        await using var scope = _serviceProvider.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IDeadLetterMessageRepository>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        await ExecuteCleanupAsync(repository, configuration, logger, ct);
    }

    /// <summary>
    /// 执行 DLQ 清理逻辑：先确认本地已有入库副本，再调用 RabbitMQ Management API 清空 DLQ 队列。
    /// 标记为 internal 以便单元测试直接调用，绕过 Quartz 上下文。
    /// </summary>
    /// <param name="repository">死信消息仓储。</param>
    /// <param name="configuration">配置。</param>
    /// <param name="logger">日志器。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>实际清理的队列数量。</returns>
    internal static async Task<int> ExecuteCleanupAsync(
        IDeadLetterMessageRepository repository,
        IConfiguration configuration,
        ILogger<DlqCleanupJob> logger,
        CancellationToken ct)
    {
        // 确认本地已有入库副本，避免清理未入库消息导致丢失
        var persistedCount = await repository.CountAsync(null, null, ct);
        if (persistedCount == 0)
        {
            logger.LogInformation("DLQ 清理作业跳过：本地无已入库死信消息副本，不清理 DLQ 避免丢失未入库消息");
            return 0;
        }

        logger.LogInformation("DLQ 清理作业开始，本地已入库死信消息副本数={PersistedCount}", persistedCount);

        var queueNames = GetQueueNames(configuration);
        var baseUrl = GetManagementApiBaseUrl(configuration);
        var vhost = GetVHost(configuration);

        using var httpClient = CreateHttpClient(configuration);

        return await PurgeQueuesAsync(httpClient, baseUrl, vhost, queueNames, logger, ct);
    }

    /// <summary>
    /// 使用指定的 HttpClient 清理多个 DLQ 队列。
    /// 标记为 internal 以便单元测试注入自定义 <see cref="HttpClient"/>（含 stub <see cref="HttpMessageHandler"/>）。
    /// </summary>
    /// <param name="httpClient">已配置 Basic Auth 的 HttpClient。</param>
    /// <param name="baseUrl">Management API 基础 URL。</param>
    /// <param name="vhost">虚拟主机。</param>
    /// <param name="queueNames">待清理的队列名称列表。</param>
    /// <param name="logger">日志器。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>实际清理的队列数量。</returns>
    internal static async Task<int> PurgeQueuesAsync(
        HttpClient httpClient,
        string baseUrl,
        string vhost,
        List<string> queueNames,
        ILogger<DlqCleanupJob> logger,
        CancellationToken ct)
    {
        var purgedCount = 0;
        foreach (var queueName in queueNames)
        {
            var purged = await PurgeDlqQueueAsync(httpClient, baseUrl, vhost, queueName, logger, ct);
            if (purged)
            {
                purgedCount++;
            }
        }

        logger.LogInformation("DLQ 清理作业完成，清理队列数={PurgedCount}/{TotalQueues}", purgedCount, queueNames.Count);
        return purgedCount;
    }

    /// <summary>
    /// 调用 RabbitMQ Management API 清空指定 DLQ 队列。
    /// 标记为 internal 以便单元测试直接调用，注入自定义 <see cref="HttpClient"/>。
    /// </summary>
    /// <param name="httpClient">已配置 Basic Auth 的 HttpClient。</param>
    /// <param name="baseUrl">Management API 基础 URL。</param>
    /// <param name="vhost">虚拟主机。</param>
    /// <param name="queueName">队列名称。</param>
    /// <param name="logger">日志器。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>是否成功清理。</returns>
    internal static async Task<bool> PurgeDlqQueueAsync(
        HttpClient httpClient,
        string baseUrl,
        string vhost,
        string queueName,
        ILogger<DlqCleanupJob> logger,
        CancellationToken ct)
    {
        var url = $"{baseUrl}/api/queues/{Uri.EscapeDataString(vhost)}/{Uri.EscapeDataString(queueName)}/contents";

        logger.LogDebug("清理 DLQ 队列：URL={Url}", url);

        try
        {
            var response = await httpClient.DeleteAsync(url, ct);

            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("DLQ 队列 {QueueName} 已清空", queueName);
                return true;
            }

            // 404 表示队列不存在（可能尚未创建死信），记录警告但不视为失败
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                logger.LogWarning("DLQ 队列 {QueueName} 不存在（404），跳过清理", queueName);
                return false;
            }

            var errorBody = await response.Content.ReadAsStringAsync(ct);
            logger.LogWarning("清理 DLQ 队列 {QueueName} 失败：StatusCode={StatusCode}, Body={Body}",
                queueName, (int)response.StatusCode, errorBody);
            return false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "清理 DLQ 队列 {QueueName} 异常", queueName);
            return false;
        }
    }

    /// <summary>
    /// 从配置读取待清理的 DLQ 队列名称列表，逗号分隔，默认 <c>dead-letter-queue</c>。
    /// </summary>
    internal static List<string> GetQueueNames(IConfiguration configuration)
    {
        var queueNamesValue = configuration[QueueNamesConfigKey];
        if (string.IsNullOrWhiteSpace(queueNamesValue))
        {
            return [DefaultQueueName];
        }

        var names = queueNamesValue
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToList();

        return names.Count == 0 ? [DefaultQueueName] : names;
    }

    /// <summary>
    /// 从配置读取 RabbitMQ Management API 基础 URL，默认 <c>http://localhost:15672</c>。
    /// </summary>
    internal static string GetManagementApiBaseUrl(IConfiguration configuration)
    {
        var section = configuration.GetSection(ManagementApiConfigKey);
        var host = section["Host"] ?? "http://localhost:15672";
        return host.TrimEnd('/');
    }

    /// <summary>
    /// 从配置读取 RabbitMQ 虚拟主机，默认 <c>%2F</c>（即 /）。
    /// </summary>
    internal static string GetVHost(IConfiguration configuration)
    {
        var section = configuration.GetSection(ManagementApiConfigKey);
        return section["VHost"] ?? "%2F";
    }

    /// <summary>
    /// 根据配置创建已配置 Basic Auth 的 HttpClient。
    /// </summary>
    internal static HttpClient CreateHttpClient(IConfiguration configuration)
    {
        var section = configuration.GetSection(ManagementApiConfigKey);
        var username = section["Username"] ?? "guest";
        var password = section["Password"] ?? "guest";

        var authBytes = Encoding.UTF8.GetBytes($"{username}:{password}");
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
        return client;
    }
}
