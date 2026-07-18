using Leno.Infrastructure.ReadModel;
using Leno.SharedContracts.Events;
using Microsoft.Extensions.Logging;

namespace Leno.PointsMembership.Infrastructure.ReadModels;

/// <summary>
/// 会员档案创建读模型同步消费者：消费 <see cref="MemberRegisteredEvent"/>，
/// 将会员档案投影为 <see cref="MemberReadModel"/> 索引到 Elasticsearch。
/// 索引失败抛出异常以触发 MassTransit 重试与死信队列。
/// 幂等：ES 索引以会员标识为 _id，重复索引为覆盖更新。
/// </summary>
public sealed class MemberRegisteredReadModelSyncConsumer
    : ReadModelSyncConsumerBase<MemberRegisteredEvent, MemberReadModel>
{
    public MemberRegisteredReadModelSyncConsumer(
        IEsReadModelRepository<MemberReadModel> repository,
        ILogger<MemberRegisteredReadModelSyncConsumer> logger)
        : base(repository, logger)
    {
    }

    /// <inheritdoc />
    protected override Task<(string Id, string IndexName, MemberReadModel? ReadModel)> BuildReadModelAsync(
        MemberRegisteredEvent integrationEvent, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var readModel = new MemberReadModel
        {
            MemberId = integrationEvent.MemberId,
            UserId = integrationEvent.UserId,
            Level = integrationEvent.Level,
            TotalConsumption = 0,
            GrowthValue = 0,
            GrowthLevel = 0,
            RegisteredAt = integrationEvent.RegisteredAt,
            LastUpgradeAt = integrationEvent.RegisteredAt,
            Status = "Active",
            IndexedAt = now,
            SchemaVersion = 1
        };

        return Task.FromResult<(string, string, MemberReadModel?)>(
            (integrationEvent.MemberId.ToString(), MemberReadModel.MemberIndexName, readModel));
    }
}
