using Leno.Infrastructure.ReadModel;
using Leno.SharedContracts.Events;
using Microsoft.Extensions.Logging;

namespace Leno.Promotion.Infrastructure.ReadModels;

/// <summary>
/// 秒杀活动发布读模型同步消费者：消费 <see cref="SeckillActivityPublishedEvent"/>，
/// 将秒杀活动投影为 <see cref="SeckillActivityReadModel"/> 索引到 Elasticsearch。
/// 索引失败抛出异常以触发 MassTransit 重试与死信队列。
/// 幂等：ES 索引以活动标识为 _id，重复索引为覆盖更新。
/// </summary>
public sealed class SeckillActivityPublishedReadModelSyncConsumer
    : ReadModelSyncConsumerBase<SeckillActivityPublishedEvent, SeckillActivityReadModel>
{
    public SeckillActivityPublishedReadModelSyncConsumer(
        IEsReadModelRepository<SeckillActivityReadModel> repository,
        ILogger<SeckillActivityPublishedReadModelSyncConsumer> logger)
        : base(repository, logger)
    {
    }

    /// <inheritdoc />
    protected override Task<(string Id, string IndexName, SeckillActivityReadModel? ReadModel)> BuildReadModelAsync(
        SeckillActivityPublishedEvent integrationEvent, CancellationToken ct)
    {
        var readModel = new SeckillActivityReadModel
        {
            ActivityId = integrationEvent.ActivityId,
            SpuId = integrationEvent.SpuId,
            SkuId = integrationEvent.SkuId,
            OriginalPrice = integrationEvent.OriginalPrice,
            SeckillPrice = integrationEvent.SeckillPrice,
            StartTime = integrationEvent.StartTime,
            EndTime = integrationEvent.EndTime,
            Status = integrationEvent.Status,
            TotalStock = integrationEvent.TotalStock,
            AvailableStock = integrationEvent.TotalStock,
            IndexedAt = DateTime.UtcNow,
            SchemaVersion = 1
        };

        return Task.FromResult<(string, string, SeckillActivityReadModel?)>(
            (integrationEvent.ActivityId.ToString(), SeckillActivityReadModel.SeckillActivityIndexName, readModel));
    }
}
