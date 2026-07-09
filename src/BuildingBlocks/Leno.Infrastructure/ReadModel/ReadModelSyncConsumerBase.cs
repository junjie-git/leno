using Leno.SharedContracts.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Leno.Infrastructure.ReadModel;

/// <summary>
/// 读模型同步消费者基类，消费集成事件并将读模型索引到 Elasticsearch。
/// 子类实现 <see cref="BuildReadModelAsync"/> 将事件转换为读模型文档与索引信息。
/// 索引失败抛出异常以触发 MassTransit 重试与死信队列。
/// </summary>
/// <typeparam name="TEvent">触发同步的集成事件类型。</typeparam>
/// <typeparam name="TReadModel">ES 读模型文档类型。</typeparam>
public abstract class ReadModelSyncConsumerBase<TEvent, TReadModel> : IConsumer<TEvent>
    where TEvent : class, IIntegrationEvent
    where TReadModel : class
{
    protected IEsReadModelRepository<TReadModel> Repository { get; }

    protected ILogger Logger { get; }

    protected ReadModelSyncConsumerBase(IEsReadModelRepository<TReadModel> repository, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(logger);
        Repository = repository;
        Logger = logger;
    }

    public async Task Consume(ConsumeContext<TEvent> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var evt = context.Message;

        try
        {
            var (id, indexName, readModel) = await BuildReadModelAsync(evt, context.CancellationToken);
            if (readModel is null || string.IsNullOrEmpty(id) || string.IsNullOrEmpty(indexName))
            {
                Logger.LogDebug("读模型构建为空，跳过同步 EventId={EventId}", evt.EventId);
                return;
            }

            var success = await Repository.IndexAsync(readModel, id, indexName, context.CancellationToken);
            if (!success)
            {
                throw new InvalidOperationException($"ES 读模型索引失败 Id={id} Index={indexName}");
            }

            Logger.LogInformation("读模型已同步 EventId={EventId} Index={Index} Id={Id}",
                evt.EventId, indexName, id);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "读模型同步失败 EventId={EventId} Type={EventType}",
                evt.EventId, typeof(TEvent).Name);
            throw;
        }
    }

    /// <summary>
    /// 由集成事件构建读模型文档及索引信息。返回 null 文档表示跳过本次同步。
    /// </summary>
    protected abstract Task<(string Id, string IndexName, TReadModel? ReadModel)> BuildReadModelAsync(
        TEvent integrationEvent, CancellationToken ct);
}
