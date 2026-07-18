using Leno.Infrastructure.ReadModel;
using Leno.PointsMembership.Domain.Repositories;
using Leno.SharedContracts.Events;
using Microsoft.Extensions.Logging;

namespace Leno.PointsMembership.Infrastructure.ReadModels;

/// <summary>
/// 积分账户余额变更读模型同步消费者：消费 <see cref="PointsAdjustedEvent"/>，
/// 注入 <see cref="IPointsAccountRepository"/> 查询最新积分账户聚合根，重建 <see cref="PointsAccountReadModel"/>
/// 并通过 IndexAsync 覆盖更新到 Elasticsearch（不删除）。
/// 索引失败抛出异常以触发 MassTransit 重试与死信队列；聚合根不存在时跳过同步。
/// 幂等：ES 索引以账户标识为 _id，重复索引为覆盖更新。
/// </summary>
public sealed class PointsAdjustedReadModelSyncConsumer
    : ReadModelSyncConsumerBase<PointsAdjustedEvent, PointsAccountReadModel>
{
    private readonly IPointsAccountRepository _accountRepository;

    public PointsAdjustedReadModelSyncConsumer(
        IEsReadModelRepository<PointsAccountReadModel> repository,
        IPointsAccountRepository accountRepository,
        ILogger<PointsAdjustedReadModelSyncConsumer> logger)
        : base(repository, logger)
    {
        _accountRepository = accountRepository;
    }

    /// <inheritdoc />
    /// <remarks>余额变更事件触发索引重建（按最新聚合根快照），不触发删除。</remarks>
    protected override async Task<(string Id, string IndexName, PointsAccountReadModel? ReadModel)> BuildReadModelAsync(
        PointsAdjustedEvent integrationEvent, CancellationToken ct)
    {
        var account = await _accountRepository.GetByIdAsync(integrationEvent.PointsAccountId, ct);
        if (account is null)
        {
            Logger.LogWarning("积分账户 {PointsAccountId} 不存在，跳过读模型同步", integrationEvent.PointsAccountId);
            return (string.Empty, string.Empty, null);
        }

        var readModel = new PointsAccountReadModel
        {
            PointsAccountId = account.Id,
            UserId = account.UserId,
            Balance = account.Balance,
            FrozenAmount = account.FrozenBalance,
            TotalEarned = account.TotalEarned,
            TotalSpent = account.TotalSpent,
            LastAdjustedAt = integrationEvent.AdjustedAt,
            Status = "Active",
            IndexedAt = DateTime.UtcNow,
            SchemaVersion = 1
        };

        return (account.Id.ToString(), PointsAccountReadModel.PointsAccountIndexName, readModel);
    }

    /// <inheritdoc />
    /// <remarks>余额变更事件仅触发索引重建，不删除读模型。</remarks>
    protected override Task<(string Id, string IndexName)?> BuildDeleteActionAsync(
        PointsAdjustedEvent integrationEvent, CancellationToken ct)
        => Task.FromResult<(string, string)?>(null);
}
