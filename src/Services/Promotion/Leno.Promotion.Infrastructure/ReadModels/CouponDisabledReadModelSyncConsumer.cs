using Leno.Infrastructure.ReadModel;
using Leno.SharedContracts.Events;
using Microsoft.Extensions.Logging;

namespace Leno.Promotion.Infrastructure.ReadModels;

/// <summary>
/// 优惠券停用读模型同步消费者：消费 <see cref="CouponDisabledEvent"/>，
/// 从 Elasticsearch 删除对应读模型文档，保证用户端不再检索到已停用的券模板。
/// 删除失败抛出异常以触发 MassTransit 重试与死信队列；文档不存在视为成功（幂等）。
/// </summary>
public sealed class CouponDisabledReadModelSyncConsumer
    : ReadModelSyncConsumerBase<CouponDisabledEvent, CouponReadModel>
{
    public CouponDisabledReadModelSyncConsumer(
        IEsReadModelRepository<CouponReadModel> repository,
        ILogger<CouponDisabledReadModelSyncConsumer> logger)
        : base(repository, logger)
    {
    }

    /// <inheritdoc />
    /// <remarks>停用事件仅触发删除，不索引读模型。</remarks>
    protected override Task<(string Id, string IndexName, CouponReadModel? ReadModel)> BuildReadModelAsync(
        CouponDisabledEvent integrationEvent, CancellationToken ct)
        => Task.FromResult<(string, string, CouponReadModel?)>((string.Empty, string.Empty, null));

    /// <inheritdoc />
    protected override Task<(string Id, string IndexName)?> BuildDeleteActionAsync(
        CouponDisabledEvent integrationEvent, CancellationToken ct)
        => Task.FromResult<(string, string)?>(
            (integrationEvent.CouponId.ToString(), CouponReadModel.CouponIndexName));
}
