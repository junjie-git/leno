using Leno.Infrastructure.ReadModel;
using Leno.SharedContracts.Events;
using Microsoft.Extensions.Logging;

namespace Leno.PointsMembership.Infrastructure.ReadModels;

/// <summary>
/// 积分账户创建读模型同步消费者：消费 <see cref="PointsAccountCreatedEvent"/>，
/// 将积分账户投影为 <see cref="PointsAccountReadModel"/> 索引到 Elasticsearch。
/// 索引失败抛出异常以触发 MassTransit 重试与死信队列。
/// 幂等：ES 索引以账户标识为 _id，重复索引为覆盖更新。
/// </summary>
public sealed class PointsAccountCreatedReadModelSyncConsumer
    : ReadModelSyncConsumerBase<PointsAccountCreatedEvent, PointsAccountReadModel>
{
    public PointsAccountCreatedReadModelSyncConsumer(
        IEsReadModelRepository<PointsAccountReadModel> repository,
        ILogger<PointsAccountCreatedReadModelSyncConsumer> logger)
        : base(repository, logger)
    {
    }

    /// <inheritdoc />
    protected override Task<(string Id, string IndexName, PointsAccountReadModel? ReadModel)> BuildReadModelAsync(
        PointsAccountCreatedEvent integrationEvent, CancellationToken ct)
    {
        var readModel = new PointsAccountReadModel
        {
            PointsAccountId = integrationEvent.PointsAccountId,
            UserId = integrationEvent.UserId,
            Balance = integrationEvent.InitialPoints,
            FrozenAmount = 0,
            TotalEarned = integrationEvent.InitialPoints,
            TotalSpent = 0,
            LastAdjustedAt = null,
            Status = "Active",
            IndexedAt = DateTime.UtcNow,
            SchemaVersion = 1
        };

        return Task.FromResult<(string, string, PointsAccountReadModel?)>(
            (integrationEvent.PointsAccountId.ToString(), PointsAccountReadModel.PointsAccountIndexName, readModel));
    }
}
