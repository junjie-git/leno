using System.Data;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Services;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SystemAdmin.Infrastructure.Options;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.SystemAdmin.Infrastructure.Services;

/// <summary>
/// Outbox 跨域查询服务默认实现。
/// 通过 <see cref="OutboxMonitorOptions.Contexts"/> 配置各域数据库连接字符串，
/// 直接 SQL 查询各域 outbox_messages 表（与 <see cref="Leno.Infrastructure.Outbox.OutboxMessage"/> 表结构一致）。
/// 重投通过将消息状态回退到 Pending 实现等效触发 OutboxPublisher 重新发布。
/// 归档通过删除积压事件实现（陈旧事件已无业务价值，归档历史记录在 outbox_archive_records 表）。
/// 任一域查询失败时记录错误日志并跳过该域，不影响其他域查询结果。
/// </summary>
public sealed class OutboxQueryService : IOutboxQueryService
{
    private const int MaxBatchRepublish = 1000;
    private const int MaxBatchArchive = 5000;

    private readonly IOptionsMonitor<OutboxMonitorOptions> _options;
    private readonly ILogger<OutboxQueryService> _logger;

    public OutboxQueryService(
        IOptionsMonitor<OutboxMonitorOptions> options,
        ILogger<OutboxQueryService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<OutboxContextSummary>> GetSummaryAsync(CancellationToken ct = default)
    {
        var opts = _options.CurrentValue;
        var summaries = new List<OutboxContextSummary>();

        foreach (var ctxCfg in opts.Contexts)
        {
            if (string.IsNullOrWhiteSpace(ctxCfg.ConnectionString))
            {
                _logger.LogDebug("跳过上下文 {Context}：连接字符串为空", ctxCfg.Context);
                continue;
            }

            try
            {
                var summary = await QueryContextSummaryAsync(ctxCfg.Context, ctxCfg.ConnectionString, opts, ct);
                summaries.Add(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "查询上下文 {Context} 的 Outbox 积压汇总失败", ctxCfg.Context);
                summaries.Add(new OutboxContextSummary
                {
                    Context = ctxCfg.Context,
                    PendingCount = 0,
                    OldestPendingAt = null,
                    MaxAgeMinutes = 0,
                    LastArchivedAt = null,
                    Status = OutboxContextStatus.Normal
                });
            }
        }

        return summaries;
    }

    /// <inheritdoc />
    public async Task<List<OutboxTrendPoint>> GetTrendAsync(int hours, CancellationToken ct = default)
    {
        var opts = _options.CurrentValue;
        var normalizedHours = hours < 1 ? 24 : hours;
        var intervalMinutes = opts.TrendSampleIntervalMinutes <= 0 ? 30 : opts.TrendSampleIntervalMinutes;
        var points = new List<OutboxTrendPoint>();
        var now = DateTime.UtcNow;
        var start = now.AddHours(-normalizedHours);

        // 按采样间隔生成时间点
        var sampleCount = normalizedHours * 60 / intervalMinutes;
        if (sampleCount < 1)
        {
            sampleCount = 1;
        }

        foreach (var ctxCfg in opts.Contexts)
        {
            if (string.IsNullOrWhiteSpace(ctxCfg.ConnectionString))
            {
                continue;
            }

            try
            {
                var contextPoints = await QueryContextTrendAsync(ctxCfg.Context, ctxCfg.ConnectionString, start, now, intervalMinutes, ct);
                points.AddRange(contextPoints);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "查询上下文 {Context} 的 Outbox 积压趋势失败", ctxCfg.Context);
            }
        }

        return points;
    }

    /// <inheritdoc />
    public async Task<OutboxMessageQueryResult> GetMessagesAsync(
        string context,
        string? status,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var ctxCfg = ResolveContext(context);
        if (ctxCfg is null)
        {
            return new OutboxMessageQueryResult { Items = new List<OutboxMessageEntry>(), Total = 0 };
        }

        var normalizedPage = page < 1 ? 1 : page;
        var normalizedPageSize = pageSize < 1 ? 20 : pageSize;

        try
        {
            await using var connection = new SqlConnection(ctxCfg.ConnectionString);
            await connection.OpenAsync(ct);

            var items = await QueryMessagesAsync(connection, context, status, normalizedPage, normalizedPageSize, ct);
            var total = await CountMessagesAsync(connection, context, status, ct);

            return new OutboxMessageQueryResult
            {
                Items = items,
                Total = total
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查询上下文 {Context} 的 Outbox 积压事件失败", context);
            return new OutboxMessageQueryResult { Items = new List<OutboxMessageEntry>(), Total = 0 };
        }
    }

    /// <inheritdoc />
    public async Task<OutboxRepublishResult> RepublishAsync(
        string context,
        IReadOnlyCollection<Guid>? messageIds,
        string operatorId,
        CancellationToken ct = default)
    {
        var ctxCfg = ResolveContext(context);
        if (ctxCfg is null)
        {
            return new OutboxRepublishResult
            {
                SuccessCount = 0,
                FailureCount = 0,
                Errors = new List<OutboxRepublishError>()
            };
        }

        var result = new OutboxRepublishResult
        {
            Errors = new List<OutboxRepublishError>()
        };

        try
        {
            await using var connection = new SqlConnection(ctxCfg.ConnectionString);
            await connection.OpenAsync(ct);

            // 不传 messageIds 时重投全部积压事件，分批处理避免单次事务过长
            if (messageIds is null || messageIds.Count == 0)
            {
                var batchIds = await LoadBacklogIdsAsync(connection, MaxBatchRepublish, ct);
                foreach (var batch in batchIds.Chunk(MaxBatchRepublish))
                {
                    var (success, failure, errors) = await RepublishBatchAsync(connection, batch.ToList(), ct);
                    result = MergeRepublishResult(result, success, failure, errors);
                }
            }
            else
            {
                var (success, failure, errors) = await RepublishBatchAsync(connection, messageIds.ToList(), ct);
                result = MergeRepublishResult(result, success, failure, errors);
            }

            _logger.LogInformation(
                "重投 Outbox 积压事件 Context={Context} OperatorId={OperatorId} Success={Success} Failure={Failure}",
                context, operatorId, result.SuccessCount, result.FailureCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "重投 Outbox 积压事件失败 Context={Context}", context);
            result.FailureCount += messageIds?.Count ?? 0;
            if (messageIds is not null)
            {
                result.Errors.AddRange(messageIds.Select(id => new OutboxRepublishError
                {
                    MessageId = id,
                    Error = ex.Message
                }));
            }
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<int> ArchiveAsync(
        string context,
        DateTime before,
        string operatorId,
        string reason,
        CancellationToken ct = default)
    {
        var ctxCfg = ResolveContext(context);
        if (ctxCfg is null)
        {
            return 0;
        }

        try
        {
            await using var connection = new SqlConnection(ctxCfg.ConnectionString);
            await connection.OpenAsync(ct);

            var totalArchived = 0;
            while (!ct.IsCancellationRequested)
            {
                var batchIds = await LoadArchiveBatchIdsAsync(connection, before, MaxBatchArchive, ct);
                if (batchIds.Count == 0)
                {
                    break;
                }

                var deleted = await DeleteBatchAsync(connection, batchIds, ct);
                totalArchived += deleted;

                if (batchIds.Count < MaxBatchArchive)
                {
                    break;
                }
            }

            _logger.LogInformation(
                "归档 Outbox 积压事件 Context={Context} Before={Before} ArchivedCount={Count} OperatorId={OperatorId}",
                context, before, totalArchived, operatorId);

            return totalArchived;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "归档 Outbox 积压事件失败 Context={Context}", context);
            return 0;
        }
    }

    private OutboxContextConfig? ResolveContext(string context)
    {
        if (string.IsNullOrWhiteSpace(context))
        {
            return null;
        }
        var opts = _options.CurrentValue;
        return opts.Contexts.FirstOrDefault(c =>
            string.Equals(c.Context, context.Trim(), StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(c.ConnectionString));
    }

    private async Task<OutboxContextSummary> QueryContextSummaryAsync(
        string context,
        string connectionString,
        OutboxMonitorOptions opts,
        CancellationToken ct)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);

        // 积压事件：Status IN ('Pending','Publishing','DeadLetter') AND ProcessedAt IS NULL
        // 最早积压时间：MIN(OccurredAt)
        // 最大积压时长：DATEDIFF(MINUTE, MIN(OccurredAt), GETUTCDATE())
        const string sql = @"
SELECT
    COUNT(1) AS pending_count,
    MIN(occurred_at) AS oldest_pending_at
FROM outbox_messages
WHERE status IN ('Pending','Publishing','DeadLetter') AND processed_at IS NULL;
";

        await using var cmd = new SqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        int pendingCount = 0;
        DateTime? oldestPendingAt = null;

        if (await reader.ReadAsync(ct))
        {
            pendingCount = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
            oldestPendingAt = reader.IsDBNull(1) ? null : reader.GetDateTime(1);
        }

        var maxAgeMinutes = oldestPendingAt.HasValue
            ? (long)(DateTime.UtcNow - oldestPendingAt.Value).TotalMinutes
            : 0L;
        if (maxAgeMinutes < 0)
        {
            maxAgeMinutes = 0;
        }

        var status = ClassifyStatus(pendingCount, opts);

        return new OutboxContextSummary
        {
            Context = context,
            PendingCount = pendingCount,
            OldestPendingAt = oldestPendingAt,
            MaxAgeMinutes = maxAgeMinutes,
            LastArchivedAt = null, // LastArchivedAt 由 AppService 从归档历史仓储补全
            Status = status
        };
    }

    private async Task<List<OutboxTrendPoint>> QueryContextTrendAsync(
        string context,
        string connectionString,
        DateTime start,
        DateTime end,
        int intervalMinutes,
        CancellationToken ct)
    {
        // 按时间桶聚合积压事件数：统计 occurred_at 落在每个时间桶内、当前仍为积压状态的事件数
        // 使用 DATEDIFF 计算桶号，GROUP BY 桶号
        const string sql = @"
SELECT
    DATEADD(MINUTE, (DATEDIFF(MINUTE, @start, occurred_at) / @interval) * @interval, @start) AS bucket,
    COUNT(1) AS cnt
FROM outbox_messages
WHERE occurred_at >= @start AND occurred_at <= @end
    AND status IN ('Pending','Publishing','DeadLetter') AND processed_at IS NULL
GROUP BY (DATEDIFF(MINUTE, @start, occurred_at) / @interval)
ORDER BY bucket;
";

        var points = new List<OutboxTrendPoint>();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.Add(new SqlParameter("@start", SqlDbType.DateTime2) { Value = start });
        cmd.Parameters.Add(new SqlParameter("@end", SqlDbType.DateTime2) { Value = end });
        cmd.Parameters.Add(new SqlParameter("@interval", SqlDbType.Int) { Value = intervalMinutes });

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var bucket = reader.GetDateTime(0);
            var count = reader.GetInt32(1);
            points.Add(new OutboxTrendPoint
            {
                Timestamp = bucket,
                Context = context,
                PendingCount = count
            });
        }

        return points;
    }

    private async Task<List<OutboxMessageEntry>> QueryMessagesAsync(
        SqlConnection connection,
        string context,
        string? status,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        var where = BuildStatusWhere(status);
        var sql = $@"
SELECT TOP (@pageSize) id, aggregate_root_id, type, payload, status, retry_count, error, occurred_at, processed_at
FROM (
    SELECT id, aggregate_root_id, type, payload, status, retry_count, error, occurred_at, processed_at,
           ROW_NUMBER() OVER (ORDER BY occurred_at DESC) AS rn
    FROM outbox_messages
    WHERE {where}
) AS t
WHERE t.rn > @offset
ORDER BY t.rn;
";

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.Add(new SqlParameter("@pageSize", SqlDbType.Int) { Value = pageSize });
        cmd.Parameters.Add(new SqlParameter("@offset", SqlDbType.Int) { Value = (page - 1) * pageSize });

        var items = new List<OutboxMessageEntry>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var id = reader.GetGuid(0);
            var aggregateRootId = reader.IsDBNull(1) ? Guid.Empty : reader.GetGuid(1);
            var type = reader.GetString(2);
            var payload = reader.GetString(3);
            var statusStr = reader.GetString(4);
            var retryCount = reader.IsDBNull(5) ? 0 : reader.GetInt32(5);
            var error = reader.IsDBNull(6) ? null : reader.GetString(6);
            var occurredAt = reader.GetDateTime(7);
            var processedAt = reader.IsDBNull(8) ? null : (DateTime?)reader.GetDateTime(8);

            items.Add(OutboxMessageEntry.Create(
                id, context, aggregateRootId, type, payload, statusStr, retryCount, error, occurredAt, processedAt));
        }

        return items;
    }

    private async Task<int> CountMessagesAsync(
        SqlConnection connection,
        string context,
        string? status,
        CancellationToken ct)
    {
        var where = BuildStatusWhere(status);
        var sql = $"SELECT COUNT(1) FROM outbox_messages WHERE {where};";

        await using var cmd = new SqlCommand(sql, connection);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is int count ? count : 0;
    }

    private static string BuildStatusWhere(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return "status IN ('Pending','Publishing','DeadLetter') AND processed_at IS NULL";
        }
        var normalized = status.Trim();
        // 防止 SQL 注入：仅允许白名单状态值
        return normalized.ToLowerInvariant() switch
        {
            "pending" => "status = 'Pending' AND processed_at IS NULL",
            "publishing" => "status = 'Publishing' AND processed_at IS NULL",
            "processed" => "status = 'Processed' AND processed_at IS NOT NULL",
            "deadletter" => "status = 'DeadLetter'",
            _ => "status IN ('Pending','Publishing','DeadLetter') AND processed_at IS NULL"
        };
    }

    private async Task<List<Guid>> LoadBacklogIdsAsync(
        SqlConnection connection,
        int maxCount,
        CancellationToken ct)
    {
        var sql = @"
SELECT TOP (@max) id
FROM outbox_messages
WHERE status IN ('Pending','Publishing','DeadLetter') AND processed_at IS NULL
ORDER BY occurred_at;
";

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.Add(new SqlParameter("@max", SqlDbType.Int) { Value = maxCount });

        var ids = new List<Guid>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            ids.Add(reader.GetGuid(0));
        }
        return ids;
    }

    private async Task<(int Success, int Failure, List<OutboxRepublishError> Errors)> RepublishBatchAsync(
        SqlConnection connection,
        List<Guid> ids,
        CancellationToken ct)
    {
        var success = 0;
        var failure = 0;
        var errors = new List<OutboxRepublishError>();

        // 重投：将状态回退为 Pending、清除 publishing_started_at 与 error，等效触发 OutboxPublisher 重新发布
        // 逐条执行避免 IN 子句参数过多；单条失败不影响其他条
        const string sql = @"
UPDATE outbox_messages
SET status = 'Pending',
    publishing_started_at = NULL,
    error = NULL
WHERE id = @id AND status IN ('Pending','Publishing','DeadLetter') AND processed_at IS NULL;
";

        foreach (var id in ids)
        {
            try
            {
                await using var cmd = new SqlCommand(sql, connection);
                cmd.Parameters.Add(new SqlParameter("@id", SqlDbType.UniqueIdentifier) { Value = id });
                var affected = await cmd.ExecuteNonQueryAsync(ct);
                if (affected > 0)
                {
                    success++;
                }
                else
                {
                    // 0 行受影响：消息不存在、已处理或状态不符，视为幂等跳过
                    success++;
                }
            }
            catch (Exception ex)
            {
                failure++;
                errors.Add(new OutboxRepublishError
                {
                    MessageId = id,
                    Error = ex.Message
                });
            }
        }

        return (success, failure, errors);
    }

    private async Task<List<Guid>> LoadArchiveBatchIdsAsync(
        SqlConnection connection,
        DateTime before,
        int maxCount,
        CancellationToken ct)
    {
        var sql = @"
SELECT TOP (@max) id
FROM outbox_messages
WHERE occurred_at < @before
    AND status IN ('Pending','Publishing','DeadLetter')
    AND processed_at IS NULL
ORDER BY occurred_at;
";

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.Add(new SqlParameter("@max", SqlDbType.Int) { Value = maxCount });
        cmd.Parameters.Add(new SqlParameter("@before", SqlDbType.DateTime2) { Value = before });

        var ids = new List<Guid>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            ids.Add(reader.GetGuid(0));
        }
        return ids;
    }

    private async Task<int> DeleteBatchAsync(
        SqlConnection connection,
        List<Guid> ids,
        CancellationToken ct)
    {
        if (ids.Count == 0)
        {
            return 0;
        }

        // 逐条删除避免 IN 子句参数过多
        var deleted = 0;
        const string sql = "DELETE FROM outbox_messages WHERE id = @id;";
        foreach (var id in ids)
        {
            await using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.Add(new SqlParameter("@id", SqlDbType.UniqueIdentifier) { Value = id });
            deleted += await cmd.ExecuteNonQueryAsync(ct);
        }
        return deleted;
    }

    private static OutboxContextStatus ClassifyStatus(int pendingCount, OutboxMonitorOptions opts)
    {
        if (pendingCount <= 0)
        {
            return OutboxContextStatus.Normal;
        }
        if (pendingCount >= opts.BacklogSevereThreshold)
        {
            return OutboxContextStatus.SevereBacklog;
        }
        if (pendingCount >= opts.BacklogWarningThreshold)
        {
            return OutboxContextStatus.Backlog;
        }
        return OutboxContextStatus.Normal;
    }

    private static OutboxRepublishResult MergeRepublishResult(
        OutboxRepublishResult current,
        int success,
        int failure,
        List<OutboxRepublishError> errors)
    {
        return new OutboxRepublishResult
        {
            SuccessCount = current.SuccessCount + success,
            FailureCount = current.FailureCount + failure,
            Errors = current.Errors.Concat(errors).ToList()
        };
    }
}
