using System.Data;
using System.Text;
using Leno.SystemAdmin.Infrastructure.Options;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.SystemAdmin.Infrastructure.BackgroundServices;

/// <summary>
/// Outbox 表 7 天归档后台服务（任务 2.4.7）。
/// 定时（每天 02:00 UTC 低峰期）扫描 outbox_messages 表，
/// 将 <c>ProcessedAt</c> 早于 <c>UtcNow - RetentionDays</c> 的已处理记录归档至
/// <c>outbox_messages_archive</c> 表后从原表删除，避免长期运行导致表无限增长。
/// 分批处理（每批 <see cref="OutboxArchivalOptions.BatchSize"/> 条）避免单次事务过长锁表。
/// 归档 SQL 显式含 <c>processed_at IS NOT NULL</c> 条件，防止误删未处理记录。
/// </summary>
public sealed class OutboxArchivalBackgroundService : BackgroundService
{
    /// <summary>每天归档执行时刻（UTC），2 点低峰期。</summary>
    private const int RunAtHourUtc = 2;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<OutboxArchivalOptions> _options;
    private readonly ILogger<OutboxArchivalBackgroundService> _logger;

    public OutboxArchivalBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<OutboxArchivalOptions> options,
        ILogger<OutboxArchivalBackgroundService> logger)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var nextRun = DateTime.UtcNow + CalculateDelayToNextRun();
        _logger.LogInformation(
            "Outbox 归档后台服务已启动，下次执行时间 {NextRun:yyyy-MM-ddTHH:mm:ssZ}",
            nextRun);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(CalculateDelayToNextRun(), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await ArchiveAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox 归档失败，将在下一周期重试");
            }
        }

        _logger.LogInformation("Outbox 归档后台服务已停止");
    }

    /// <summary>
    /// 执行一轮归档扫描：从 outbox_messages 表分批取出已处理且超过保留期的记录，
    /// 在事务内逐批复制到 outbox_messages_archive 后从原表删除。
    /// 任一批次失败回滚当前批次并抛出，由 <see cref="ExecuteAsync"/> 捕获后下一周期重试。
    /// </summary>
    private async Task ArchiveAsync(CancellationToken cancellationToken)
    {
        var opts = _options.CurrentValue;
        if (opts.RetentionDays <= 0)
        {
            _logger.LogDebug("RetentionDays={RetentionDays}，跳过归档", opts.RetentionDays);
            return;
        }
        if (opts.BatchSize <= 0)
        {
            _logger.LogWarning("BatchSize={BatchSize} 非法，跳过归档", opts.BatchSize);
            return;
        }

        var cutoff = DateTime.UtcNow.AddDays(-opts.RetentionDays);
        var totalArchived = 0;
        Guid? firstStartId = null;
        Guid? lastEndId = null;

        _logger.LogInformation(
            "Outbox 归档开始扫描：cutoff={Cutoff:yyyy-MM-ddTHH:mm:ssZ}, batchSize={BatchSize}",
            cutoff, opts.BatchSize);

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SystemAdminDbContext>();

        while (!cancellationToken.IsCancellationRequested)
        {
            // 仅归档已处理（ProcessedAt 非 null）且早于 cutoff 的记录
            var batch = await dbContext.OutboxMessages
                .Where(m => m.ProcessedAt != null && m.ProcessedAt < cutoff)
                .OrderBy(m => m.Id)
                .Take(opts.BatchSize)
                .ToListAsync(cancellationToken);

            if (batch.Count == 0)
            {
                break;
            }

            var ids = batch.Select(b => b.Id).ToList();
            var batchStartId = ids[0];
            var batchEndId = ids[ids.Count - 1];
            firstStartId ??= batchStartId;
            lastEndId = batchEndId;

            await using var transaction = await dbContext.Database
                .BeginTransactionAsync(cancellationToken);

            try
            {
                var (insertSql, deleteSql, parameters) = BuildArchiveSql(ids, cutoff);

                // 1. 复制到归档表
                await dbContext.Database.ExecuteSqlRawAsync(insertSql, parameters, cancellationToken);

                // 2. 从原表删除（返回受影响行数，用于审计）
                var deletedRows = await dbContext.Database
                    .ExecuteSqlRawAsync(deleteSql, parameters, cancellationToken);

                await transaction.CommitAsync(cancellationToken);
                totalArchived += batch.Count;

                _logger.LogInformation(
                    "Outbox 归档批次完成：startId={StartId}, endId={EndId}, count={Count}, deletedRows={DeletedRows}",
                    batchStartId, batchEndId, batch.Count, deletedRows);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex,
                    "Outbox 归档批次失败已回滚：startId={StartId}, endId={EndId}, count={Count}",
                    batchStartId, batchEndId, batch.Count);
                throw;
            }
        }

        _logger.LogInformation(
            "Outbox 归档完成审计：totalArchived={TotalArchived}, firstStartId={FirstStartId}, lastEndId={LastEndId}, cutoff={Cutoff:yyyy-MM-ddTHH:mm:ssZ}",
            totalArchived, firstStartId, lastEndId, cutoff);
    }

    /// <summary>
    /// 构造归档 SQL 与参数列表。
    /// 使用 <c>id IN (...)</c> 精确匹配批次 ID，并显式加上 <c>processed_at IS NOT NULL</c> 与
    /// <c>processed_at &lt; @cutoff</c> 双重保险，防止误删未处理或近期记录。
    /// </summary>
    private static (string InsertSql, string DeleteSql, IEnumerable<object> Parameters) BuildArchiveSql(
        List<Guid> ids, DateTime cutoff)
    {
        var paramNames = new StringBuilder(ids.Count * 8);
        var parameters = new List<object>(ids.Count + 1);

        for (var i = 0; i < ids.Count; i++)
        {
            if (i > 0)
            {
                paramNames.Append(',');
            }
            var paramName = $"@id{i}";
            paramNames.Append(paramName);
            parameters.Add(new SqlParameter(paramName, ids[i]));
        }

        // 显式指定 SqlDbType.DateTime2 与 outbox_messages.processed_at 列类型对齐
        var cutoffParam = new SqlParameter("@cutoff", SqlDbType.DateTime2)
        {
            Value = cutoff
        };
        parameters.Add(cutoffParam);

        var inClause = paramNames.ToString();

        var insertSql = $@"INSERT INTO outbox_messages_archive
SELECT * FROM outbox_messages
WHERE id IN ({inClause}) AND processed_at IS NOT NULL AND processed_at < @cutoff";

        var deleteSql = $@"DELETE FROM outbox_messages
WHERE id IN ({inClause}) AND processed_at IS NOT NULL AND processed_at < @cutoff";

        return (insertSql, deleteSql, parameters);
    }

    /// <summary>
    /// 计算到下一次归档执行（每天 02:00 UTC）的延迟时间。
    /// 若当前时刻已过今天的执行点，则下次执行时间为明天 02:00 UTC。
    /// </summary>
    private static TimeSpan CalculateDelayToNextRun()
    {
        var now = DateTime.UtcNow;
        var nextRun = now.Date.AddHours(RunAtHourUtc);
        if (now >= nextRun)
        {
            nextRun = nextRun.AddDays(1);
        }
        return nextRun - now;
    }
}
