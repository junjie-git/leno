using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Leno.Infrastructure.ReadModel;

/// <summary>
/// 基于 EF Core <see cref="DbContext"/> 的 <see cref="ISnapshotStore"/> 实现。
/// 快照持久化到 <c>read_model_snapshots</c> 表，状态以 JSON 文本存储。
/// 采用泛型 <typeparamref name="TContext"/> 以适配各 BC 的 DbContext（如 OrderDbContext），
/// 由各 BC 在 DI 中注册 <c>SqlSnapshotStore&lt;TContext&gt;</c>。
/// </summary>
/// <typeparam name="TContext">承载 <see cref="ReadModelSnapshot"/> 实体的 DbContext 类型。</typeparam>
public sealed class SqlSnapshotStore<TContext> : ISnapshotStore
    where TContext : DbContext
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private readonly TContext _dbContext;
    private readonly ILogger<SqlSnapshotStore<TContext>> _logger;

    public SqlSnapshotStore(TContext dbContext, ILogger<SqlSnapshotStore<TContext>> logger)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(logger);
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Snapshot<T>?> GetLatestAsync<T>(string aggregateId, CancellationToken ct) where T : class
    {
        ArgumentException.ThrowIfNullOrEmpty(aggregateId);

        var latest = await _dbContext.Set<ReadModelSnapshot>()
            .Where(s => s.AggregateId == aggregateId)
            .OrderByDescending(s => s.Version)
            .FirstOrDefaultAsync(ct);

        if (latest is null)
        {
            return null;
        }

        var state = JsonSerializer.Deserialize<T>(latest.StateJson, SerializerOptions);
        if (state is null)
        {
            _logger.LogWarning("快照反序列化失败 AggregateId={AggregateId} Version={Version}",
                aggregateId, latest.Version);
            return null;
        }

        return new Snapshot<T>(latest.AggregateId, state, latest.Version, latest.TakenAt);
    }

    /// <inheritdoc />
    public async Task SaveAsync<T>(string aggregateId, T state, long version, CancellationToken ct) where T : class
    {
        ArgumentException.ThrowIfNullOrEmpty(aggregateId);
        ArgumentNullException.ThrowIfNull(state);
        if (version < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(version), version, "版本号不能为负。");
        }

        var aggregateType = typeof(T).Name;
        var stateJson = JsonSerializer.Serialize(state, SerializerOptions);
        var takenAt = DateTime.UtcNow;

        var existing = await _dbContext.Set<ReadModelSnapshot>()
            .FirstOrDefaultAsync(s => s.AggregateId == aggregateId && s.Version == version, ct);

        if (existing is not null)
        {
            existing.AggregateType = aggregateType;
            existing.StateJson = stateJson;
            existing.TakenAt = takenAt;
        }
        else
        {
            var snapshot = new ReadModelSnapshot
            {
                AggregateId = aggregateId,
                AggregateType = aggregateType,
                Version = version,
                StateJson = stateJson,
                TakenAt = takenAt
            };
            await _dbContext.Set<ReadModelSnapshot>().AddAsync(snapshot, ct);
        }

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogDebug("快照已保存 AggregateId={AggregateId} AggregateType={AggregateType} Version={Version}",
            aggregateId, aggregateType, version);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SnapshotDescriptor>> ListSnapshotsAsync(string aggregateType, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(aggregateType);

        var descriptors = await _dbContext.Set<ReadModelSnapshot>()
            .Where(s => s.AggregateType == aggregateType)
            .OrderByDescending(s => s.Version)
            .Select(s => new SnapshotDescriptor(s.AggregateId, s.AggregateType, s.Version, s.TakenAt))
            .ToListAsync(ct);

        return descriptors;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string aggregateId, long version, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(aggregateId);

        var snapshot = await _dbContext.Set<ReadModelSnapshot>()
            .FirstOrDefaultAsync(s => s.AggregateId == aggregateId && s.Version == version, ct);

        if (snapshot is null)
        {
            return;
        }

        _dbContext.Set<ReadModelSnapshot>().Remove(snapshot);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogDebug("快照已删除 AggregateId={AggregateId} Version={Version}", aggregateId, version);
    }
}
