using Leno.SharedContracts.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Leno.Infrastructure.ReadModel;

/// <summary>
/// 读模型同步消费者基类，消费集成事件并将读模型索引到 Elasticsearch。
/// 子类实现 <see cref="BuildReadModelAsync"/> 将事件转换为读模型文档与索引信息；
/// 重写 <see cref="BuildDeleteActionAsync"/> 声明本事件触发删除（默认返回 null，仅索引场景无需重写）。
/// 删除分支优先于索引分支：同一事件通常不会同时触发索引与删除。
/// 索引或删除失败均抛出异常以触发 MassTransit 重试与死信队列。
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
            // 删除分支（优先于索引分支：同一事件通常不会同时触发索引与删除）
            var deleteAction = await BuildDeleteActionAsync(evt, context.CancellationToken);
            if (deleteAction is { } delete
                && !string.IsNullOrEmpty(delete.Id)
                && !string.IsNullOrEmpty(delete.IndexName))
            {
                var deleteSuccess = await Repository.DeleteByIdAsync(
                    delete.Id, delete.IndexName, context.CancellationToken);
                if (!deleteSuccess)
                {
                    throw new InvalidOperationException(
                        $"ES 读模型删除失败 Id={delete.Id} Index={delete.IndexName}");
                }

                Logger.LogInformation("读模型已删除 EventId={EventId} Index={Index} Id={Id}",
                    evt.EventId, delete.IndexName, delete.Id);
                return;
            }

            // 索引分支（既有逻辑保持不变）
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

    /// <summary>
    /// 派生类重写以声明本事件需删除读模型。返回 (Id, IndexName) 触发 <see cref="IEsReadModelRepository{T}"/>.DeleteByIdAsync；
    /// 返回 null 表示本事件不触发删除（仅由 <see cref="BuildReadModelAsync"/> 决定是否索引）。
    /// 默认实现返回 null（向后兼容：仅索引场景无需重写）。
    /// </summary>
    protected virtual Task<(string Id, string IndexName)?> BuildDeleteActionAsync(
        TEvent integrationEvent, CancellationToken ct)
        => Task.FromResult<(string Id, string IndexName)?>(null);
}
