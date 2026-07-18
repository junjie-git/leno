using Leno.Infrastructure.ReadModel;
using Leno.SharedContracts.Events;
using Microsoft.Extensions.Logging;

namespace Leno.Promotion.Infrastructure.ReadModels;

/// <summary>
/// 优惠券创建读模型同步消费者：消费 <see cref="CouponCreatedEvent"/>，
/// 将优惠券模板投影为 <see cref="CouponReadModel"/> 索引到 Elasticsearch。
/// 索引失败抛出异常以触发 MassTransit 重试与死信队列。
/// 幂等：ES 索引以券模板标识为 _id，重复索引为覆盖更新。
/// </summary>
public sealed class CouponCreatedReadModelSyncConsumer
    : ReadModelSyncConsumerBase<CouponCreatedEvent, CouponReadModel>
{
    public CouponCreatedReadModelSyncConsumer(
        IEsReadModelRepository<CouponReadModel> repository,
        ILogger<CouponCreatedReadModelSyncConsumer> logger)
        : base(repository, logger)
    {
    }

    /// <inheritdoc />
    protected override Task<(string Id, string IndexName, CouponReadModel? ReadModel)> BuildReadModelAsync(
        CouponCreatedEvent integrationEvent, CancellationToken ct)
    {
        var readModel = new CouponReadModel
        {
            CouponId = integrationEvent.CouponId,
            Name = integrationEvent.Name,
            CouponType = integrationEvent.CouponType,
            FaceValue = integrationEvent.FaceValue,
            MinSpend = integrationEvent.MinSpend,
            ValidFrom = integrationEvent.ValidFrom,
            ValidTo = integrationEvent.ValidTo,
            TotalQty = integrationEvent.TotalQty,
            IssuedQty = 0,
            Status = integrationEvent.Status,
            IndexedAt = DateTime.UtcNow,
            SchemaVersion = 1
        };

        return Task.FromResult<(string, string, CouponReadModel?)>(
            (integrationEvent.CouponId.ToString(), CouponReadModel.CouponIndexName, readModel));
    }
}
