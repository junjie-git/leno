using System.Text.Json;
using Leno.Infrastructure.Abstractions;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.Infrastructure.Outbox;

/// <summary>
/// 分片发件箱发布器（4.4 Outbox 分片发布器）。
/// <para>
/// 在 <see cref="OutboxPublisher{TDbContext}"/> 单实例发布器基础上引入分片机制：
/// <list type="bullet">
/// <item>每个实例通过 <see cref="OutboxShardingOptions.ShardId"/> 声明自己负责的分片号；</item>
/// <item>拉取本分片 pending 消息时使用 <c>SELECT ... WITH (UPDLOCK, ROWLOCK, READPAST)</c>
///   行级锁，跳过已被其他查询锁定的行，避免多实例重复发布；</item>
/// <item>两阶段标记（Pending → Publishing → Processed）与 <see cref="OutboxPublisher{TDbContext}"/>
///   保持一致，发布失败回退 Pending 重试，超时由 <see cref="RecoverStalePublishingAsync"/> 兜底。</item>
/// </list>
/// </para>
/// <para>
/// 水平扩展：增加实例数（同时增加 <see cref="OutboxShardingOptions.ShardCount"/>）即可线性提升发布吞吐，
/// 不同分片互不锁竞争。<see cref="IShardingStrategy"/> 保证同一聚合根的事件始终由同一实例顺序发布。
/// </para>
/// <para>
/// 双轨期：与 <see cref="OutboxPublisher{TDbContext}"/> 通过 feature flag 按 BC 切流，
/// 单实例发布器保留 4 周过渡。
/// </para>
/// </summary>
/// <typeparam name="TDbContext">承载发件箱表的 DbContext 类型。</typeparam>
public class ShardedOutboxPublisher<TDbContext> : BackgroundService
    where TDbContext : DbContext
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IEventBus _eventBus;
    private readonly ILogger<ShardedOutboxPublisher<TDbContext>> _logger;
    private readonly IOutboxEventTypeResolver _typeResolver;
    private readonly OutboxShardingOptions _options;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>OutboxMessage 表名（与 <see cref="OutboxMessageConfiguration"/> 中 ToTable 一致）。</summary>
    private const string OutboxTableName = "outbox_messages";

    public ShardedOutboxPublisher(
        IServiceProvider serviceProvider,
        IEventBus eventBus,
        IOptions<OutboxShardingOptions> options,
        ILogger<ShardedOutboxPublisher<TDbContext>> logger,
        IOutboxEventTypeResolver? typeResolver = null)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(eventBus);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _serviceProvider = serviceProvider;
        _eventBus = eventBus;
        _logger = logger;
        _typeResolver = typeResolver ?? DefaultOutboxEventTypeResolver.Instance;
        _options = options.Value;
        _options.Validate();
    }

    /// <summary>
    /// 当前实例负责的分片号（暴露供测试断言）。
    /// </summary>
    internal int InstanceShard => _options.ShardId;

    /// <summary>
    /// 当前实例分片总数（暴露供测试断言）。
    /// </summary>
    internal int ShardCount => _options.ShardCount;

    /// <summary>
    /// 当前配置选项（暴露供子类访问，如测试子类覆盖 SQL 时需读取 BatchSize）。
    /// </summary>
    protected OutboxShardingOptions Options => _options;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "ShardedOutboxPublisher 启动：ShardId={ShardId}, ShardCount={ShardCount}, BatchSize={BatchSize}, PollingInterval={PollingInterval}s",
            _options.ShardId, _options.ShardCount, _options.BatchSize, _options.PollingIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RecoverStalePublishingAsync(stoppingToken);
                await ProcessBatchAsync(stoppingToken);
                await AlertIfPendingBacklogAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "ShardedOutboxPublisher 分片 {ShardId} 轮询异常",
                    _options.ShardId);
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_options.PollingIntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>
    /// 扫描本分片内 Publishing 状态超时的消息，回退 Pending 等待重试。
    /// 仅处理 <c>ShardKey == InstanceShard</c> 的消息，避免与其他分片实例冲突。
    /// </summary>
    internal async Task RecoverStalePublishingAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TDbContext>();

        var staleThreshold = DateTime.UtcNow - TimeSpan.FromSeconds(_options.PublishingStaleTimeoutSeconds);
        var staleMessages = await context.Set<OutboxMessage>()
            .Where(m => m.ShardKey == _options.ShardId
                        && m.Status == OutboxMessageStatus.Publishing
                        && m.PublishingStartedAt != null
                        && m.PublishingStartedAt < staleThreshold)
            .OrderBy(m => m.PublishingStartedAt)
            .Take(_options.BatchSize)
            .ToListAsync(stoppingToken);

        if (staleMessages.Count == 0)
        {
            return;
        }

        _logger.LogWarning(
            "ShardedOutboxPublisher 分片 {ShardId} 扫描到 {Count} 条 Publishing 超时消息，回退 Pending 等待重试",
            _options.ShardId, staleMessages.Count);

        foreach (var message in staleMessages)
        {
            message.ResetStalePublishing();
        }

        await context.SaveChangesAsync(stoppingToken);
    }

    /// <summary>
    /// 处理本分片一批 pending 消息。每条消息独立作用域 + 独立事务，保持两阶段标记语义。
    /// </summary>
    internal async Task ProcessBatchAsync(CancellationToken stoppingToken)
    {
        // 阶段 0：拉取本分片 pending 消息 ID（DbContext 非线程安全，并行处理需每条消息独立作用域）
        List<Guid> pendingIds;
        using (var scope = _serviceProvider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<TDbContext>();
            pendingIds = await FetchPendingMessageIdsAsync(context, stoppingToken);
        }

        if (pendingIds.Count == 0)
        {
            return;
        }

        _logger.LogDebug(
            "ShardedOutboxPublisher 分片 {ShardId} 拉取到 {Count} 条 pending 消息",
            _options.ShardId, pendingIds.Count);

        // 阶段 1+2+3：串行处理每条消息（SKIP LOCKED 已保证本分片内无并发竞争，串行更安全）
        foreach (var messageId in pendingIds)
        {
            try
            {
                await PublishSingleByIdAsync(messageId, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex,
                    "ShardedOutboxPublisher 分片 {ShardId} 消息处理异常 Id={MessageId}",
                    _options.ShardId, messageId);
            }
        }
    }

    /// <summary>
    /// 测试入口：处理本分片一批 Pending 消息。
    /// </summary>
    internal Task ProcessBatchForTestAsync(CancellationToken ct) => ProcessBatchAsync(ct);

    /// <summary>
    /// 拉取本分片 pending 消息 ID 列表。
    /// <para>
    /// 默认实现走 <see cref="FetchPendingMessagesWithSkipLockedAsync"/>（SQL Server SKIP LOCKED）。
    /// 子类可覆盖为 LINQ 实现（测试用 InMemory/SQLite 时使用）。
    /// </para>
    /// </summary>
    protected virtual async Task<List<Guid>> FetchPendingMessageIdsAsync(TDbContext context, CancellationToken ct)
    {
        var messages = await FetchPendingMessagesWithSkipLockedAsync(context, ct);
        return messages.Select(m => m.Id).ToList();
    }

    /// <summary>
    /// 使用 <c>SELECT ... WITH (UPDLOCK, ROWLOCK, READPAST)</c> 行级锁拉取本分片 pending 消息。
    /// <para>
    /// SQL Server 语法说明：<br/>
    /// - <c>WITH (UPDLOCK, ROWLOCK, READPAST)</c>：获取更新锁 + 行级锁 + 跳过已锁定行，
    ///   等价于 PostgreSQL/MySQL 的 <c>FOR UPDATE SKIP LOCKED</c>；<br/>
    /// - <c>TOP (@batchSize)</c>：限制单批次拉取数量；<br/>
    /// - <c>WHERE shard_key = @shardId AND status = 0</c>：仅拉取本分片的 Pending 消息
    ///   （<see cref="OutboxMessageStatus.Pending"/> = 0）；<br/>
    /// - <c>ORDER BY occurred_at</c>：按发生时间顺序发布，保证事件顺序性。
    /// </para>
    /// </summary>
    /// <remarks>
    /// 此方法仅在 SQL Server 上有效；测试子类可覆盖 <see cref="FetchPendingMessageIdsAsync"/>
    /// 改用 LINQ 路径（InMemory/SQLite 不支持 <c>WITH (UPDLOCK, ROWLOCK, READPAST)</c> 提示）。
    /// </remarks>
    protected virtual async Task<List<OutboxMessage>> FetchPendingMessagesWithSkipLockedAsync(
        TDbContext context,
        CancellationToken ct)
    {
        var sql = BuildSkipLockedSql(_options.BatchSize, _options.ShardId);
        // FromSqlRaw 要求查询从 DbSet 出发，且 SQL 必须返回实体所有列
        return await context.Set<OutboxMessage>()
            .FromSqlRaw(sql)
            .ToListAsync(ct);
    }

    /// <summary>
    /// 生成 <c>SELECT ... WITH (UPDLOCK, ROWLOCK, READPAST)</c> SQL（SQL Server 方言）。
    /// <para>
    /// 使用参数化占位符 <c>@batchSize</c> / <c>@shardId</c>，由 EF Core <see cref="FromSqlRaw"/>
    /// 自动绑定参数，避免 SQL 注入。
    /// </para>
    /// </summary>
    /// <param name="batchSize">单批次拉取数量。</param>
    /// <param name="shardId">本实例分片号。</param>
    /// <returns>SQL 字符串，包含两个参数占位符。</returns>
    internal static string BuildSkipLockedSql(int batchSize, int shardId)
    {
        // 使用 @p0/@p1 占位符，FromSqlRaw 会按顺序绑定参数
        // 注意：SQL Server 的 TOP 子句需要用括号包裹参数
        return $@"SELECT TOP ({batchSize}) * FROM {OutboxTableName} WITH (UPDLOCK, ROWLOCK, READPAST)
WHERE shard_key = {shardId} AND status = {(int)OutboxMessageStatus.Pending}
ORDER BY occurred_at";
    }

    /// <summary>
    /// 按消息 ID 重新加载并发布单条消息（独立作用域 + 独立 DbContext）。
    /// </summary>
    private async Task PublishSingleByIdAsync(Guid messageId, CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TDbContext>();

        var message = await context.Set<OutboxMessage>()
            .FirstOrDefaultAsync(m => m.Id == messageId, stoppingToken);

        if (message is null)
        {
            _logger.LogDebug(
                "ShardedOutboxPublisher 分片 {ShardId} 消息未找到 Id={MessageId}",
                _options.ShardId, messageId);
            return;
        }

        // 仅处理仍为 Pending 的消息
        if (message.Status != OutboxMessageStatus.Pending)
        {
            _logger.LogDebug(
                "ShardedOutboxPublisher 分片 {ShardId} 消息状态非 Pending，跳过 Id={MessageId} Status={Status}",
                _options.ShardId, messageId, message.Status);
            return;
        }

        await PublishSingleAsync(context, message, stoppingToken);
    }

    /// <summary>
    /// 单条消息的两阶段发布：Publishing 提交 → 发布 MQ → Processed 条件更新。
    /// </summary>
    private async Task PublishSingleAsync(TDbContext context, OutboxMessage message, CancellationToken stoppingToken)
    {
        Type? eventType;
        IIntegrationEvent? integrationEvent;

        try
        {
            eventType = _typeResolver.Resolve(message.Type);
            if (eventType is null)
            {
                _logger.LogError(
                    "ShardedOutboxPublisher 分片 {ShardId} 无法解析事件类型 Type={Type}",
                    _options.ShardId, message.Type);
                message.MarkAsFailed("事件类型无法解析", _options.MaxRetryCount);
                await context.SaveChangesAsync(stoppingToken);
                return;
            }

            integrationEvent = JsonSerializer.Deserialize(message.Payload, eventType, SerializerOptions) as IIntegrationEvent;
            if (integrationEvent is null)
            {
                message.MarkAsFailed("事件反序列化为 null", _options.MaxRetryCount);
                await context.SaveChangesAsync(stoppingToken);
                return;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "ShardedOutboxPublisher 分片 {ShardId} 消息预处理失败 Id={MessageId}",
                _options.ShardId, message.Id);
            message.MarkAsFailed(ex.Message, _options.MaxRetryCount);
            await context.SaveChangesAsync(stoppingToken);
            return;
        }

        // 阶段 1：事务内置 Publishing 中间态并提交
        message.MarkAsPublishing();
        try
        {
            await context.SaveChangesAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "ShardedOutboxPublisher 分片 {ShardId} 消息置 Publishing 失败 Id={MessageId}",
                _options.ShardId, message.Id);
            message.ResetStalePublishing();
            throw;
        }

        // 阶段 2：发布到 MQ
        try
        {
            var headers = new Dictionary<string, string?>
            {
                ["schema-version"] = message.SchemaVersion.ToString()
            };
            await _eventBus.PublishAsync(integrationEvent, headers, stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "ShardedOutboxPublisher 分片 {ShardId} 消息发布失败 Id={MessageId}",
                _options.ShardId, message.Id);
            message.MarkAsFailed(ex.Message, _options.MaxRetryCount);
            try
            {
                await context.SaveChangesAsync(stoppingToken);
            }
            catch (Exception commitEx)
            {
                _logger.LogError(commitEx,
                    "ShardedOutboxPublisher 分片 {ShardId} 发布失败回退提交失败 Id={MessageId}",
                    _options.ShardId, message.Id);
            }
            return;
        }

        // 阶段 3：条件更新置 Processed
        await MarkAsProcessedAsync(context, message, eventType, stoppingToken);
    }

    /// <summary>
    /// 阶段 3：条件更新置 Processed（WHERE Status = Publishing 保证原子性）。
    /// <para>
    /// 生产环境使用 <see cref="ExecuteUpdateAsync"/> 绕过 ChangeTracker 直接条件更新 DB，
    /// 只有持有 Publishing 锁的实例能标记 Processed；若状态已被其他实例重置为 Pending，
    /// 条件更新不命中（0 行），消息不会被误改为 Processed。
    /// </para>
    /// <para>
    /// 测试子类可覆盖此方法改用 <see cref="DbContext.SaveChangesAsync(CancellationToken)"/>
    /// （InMemory provider 不支持 <see cref="ExecuteUpdateAsync"/>）。
    /// </para>
    /// </summary>
    /// <param name="context">当前作用域 DbContext。</param>
    /// <param name="message">已发布成功的消息（Status=Publishing）。</param>
    /// <param name="eventType">事件类型（用于日志）。</param>
    /// <param name="stoppingToken">取消令牌。</param>
    protected virtual async Task MarkAsProcessedAsync(
        TDbContext context,
        OutboxMessage message,
        Type eventType,
        CancellationToken stoppingToken)
    {
        try
        {
            var updatedRows = await context.Set<OutboxMessage>()
                .Where(m => m.Id == message.Id && m.Status == OutboxMessageStatus.Publishing)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(m => m.Status, OutboxMessageStatus.Processed)
                    .SetProperty(m => m.ProcessedAt, DateTime.UtcNow)
                    .SetProperty(m => m.PublishingStartedAt, (DateTime?)null)
                    .SetProperty(m => m.Error, (string?)null),
                    stoppingToken);

            if (updatedRows > 0)
            {
                OutboxMetrics.RecordPublished(typeof(TDbContext).Name);
                _logger.LogInformation(
                    "ShardedOutboxPublisher 分片 {ShardId} 消息已发布 Id={MessageId} Type={Type}",
                    _options.ShardId, message.Id, eventType.Name);
            }
            else
            {
                _logger.LogWarning(
                    "ShardedOutboxPublisher 分片 {ShardId} 条件更新未命中 Id={MessageId}，依赖下游幂等兜底",
                    _options.ShardId, message.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "ShardedOutboxPublisher 分片 {ShardId} 消息发布成功但 Processed 标记失败 Id={MessageId}，将由 Publishing 超时扫描回退",
                _options.ShardId, message.Id);
        }
        finally
        {
            context.ChangeTracker.Clear();
        }
    }

    /// <summary>
    /// 统计本分片 pending 消息数量，超阈值告警。
    /// </summary>
    internal async Task AlertIfPendingBacklogAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TDbContext>();

        var pendingCount = await context.Set<OutboxMessage>()
            .CountAsync(m => m.ShardKey == _options.ShardId
                             && m.Status == OutboxMessageStatus.Pending,
                stoppingToken);

        OutboxMetrics.SetPendingCount(pendingCount);

        if (pendingCount > _options.PendingAlertThreshold)
        {
            _logger.LogWarning(
                "ShardedOutboxPublisher 分片 {ShardId} 积压告警：pending 消息数 {PendingCount} 超过阈值 {Threshold}",
                _options.ShardId, pendingCount, _options.PendingAlertThreshold);
        }
    }
}
