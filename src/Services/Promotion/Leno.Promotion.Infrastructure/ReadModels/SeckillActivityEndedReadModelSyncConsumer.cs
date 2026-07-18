using Leno.Infrastructure.ReadModel;
using Leno.SharedContracts.Events;
using Microsoft.Extensions.Logging;

namespace Leno.Promotion.Infrastructure.ReadModels;

/// <summary>
/// 秒杀活动结束读模型同步消费者：消费 <see cref="SeckillActivityEndedEvent"/>，
/// 从 Elasticsearch 删除对应读模型文档，保证前台不再检索到已结束的活动。
/// 删除失败抛出异常以触发 MassTransit 重试与死信队列；文档不存在视为成功（幂等）。
/// </summary>
public sealed class SeckillActivityEndedReadModelSyncConsumer
    : ReadModelSyncConsumerBase<SeckillActivityEndedEvent, SeckillActivityReadModel>
{
    public SeckillActivityEndedReadModelSyncConsumer(
        IEsReadModelRepository<SeckillActivityReadModel> repository,
        ILogger<SeckillActivityEndedReadModelSyncConsumer> logger)
        : base(repository, logger)
    {
    }

    /// <inheritdoc />
    /// <remarks>结束事件仅触发删除，不索引读模型。</remarks>
    protected override Task<(string Id, string IndexName, SeckillActivityReadModel? ReadModel)> BuildReadModelAsync(
        SeckillActivityEndedEvent integrationEvent, CancellationToken ct)
        => Task.FromResult<(string, string, SeckillActivityReadModel?)>((string.Empty, string.Empty, null));

    /// <inheritdoc />
    protected override Task<(string Id, string IndexName)?> BuildDeleteActionAsync(
        SeckillActivityEndedEvent integrationEvent, CancellationToken ct)
        => Task.FromResult<(string, string)?>(
            (integrationEvent.ActivityId.ToString(), SeckillActivityReadModel.SeckillActivityIndexName));
}
