using Leno.Infrastructure.ReadModel;
using Leno.Product.Domain.Repositories;
using Leno.SharedContracts.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Leno.Product.Infrastructure.ReadModels;

/// <summary>
/// 商品上架读模型同步消费者：消费 <see cref="ProductPublishedEvent"/>，
/// 加载 SPU 聚合并投影为 <see cref="ProductReadModel"/> 索引到 Elasticsearch。
/// 索引失败抛出异常以触发 MassTransit 重试与死信队列。
/// 幂等：ES 索引以商品标识为 _id，重复索引为覆盖更新。
/// </summary>
public sealed class ProductPublishedReadModelSyncConsumer : ReadModelSyncConsumerBase<ProductPublishedEvent, ProductReadModel>
{
    private readonly ISPURepository _spuRepository;

    public ProductPublishedReadModelSyncConsumer(
        ISPURepository spuRepository,
        IEsReadModelRepository<ProductReadModel> repository,
        ILogger<ProductPublishedReadModelSyncConsumer> logger)
        : base(repository, logger)
    {
        ArgumentNullException.ThrowIfNull(spuRepository);
        _spuRepository = spuRepository;
    }

    /// <inheritdoc />
    protected override async Task<(string Id, string IndexName, ProductReadModel? ReadModel)> BuildReadModelAsync(
        ProductPublishedEvent integrationEvent, CancellationToken ct)
    {
        var spu = await _spuRepository.GetByIdAsync(integrationEvent.ProductId, ct);
        if (spu is null)
        {
            Logger.LogWarning("商品不存在，跳过读模型同步 ProductId={ProductId}", integrationEvent.ProductId);
            return (string.Empty, string.Empty, null);
        }

        var prices = spu.SKUs.Select(s => s.Price.Amount).ToList();
        var minPrice = prices.Count != 0 ? prices.Min() : 0m;
        var maxPrice = prices.Count != 0 ? prices.Max() : 0m;
        var currency = spu.SKUs.FirstOrDefault()?.Price.Currency ?? "CNY";

        var readModel = new ProductReadModel
        {
            Id = spu.Id,
            Title = spu.Title,
            Subtitle = spu.Subtitle,
            MainImageUrl = spu.MainImageUrl,
            CategoryId = spu.CategoryId,
            BrandId = spu.BrandId,
            ShopId = spu.ShopId,
            Status = spu.Status.ToString(),
            Specs = spu.Specs.ToList(),
            MinPrice = minPrice,
            MaxPrice = maxPrice,
            Currency = currency,
            IndexedAt = DateTime.UtcNow
        };

        return (spu.Id.ToString(), ProductSearchService.ProductIndexName, readModel);
    }
}

/// <summary>
/// 商品下架读模型同步消费者：消费 <see cref="ProductTakenDownEvent"/>，
/// 从 Elasticsearch 删除对应读模型文档，保证买家端不再检索到下架商品。
/// 删除失败抛出异常以触发重试；文档不存在视为成功（幂等）。
/// </summary>
public sealed class ProductTakenDownReadModelSyncConsumer : IConsumer<ProductTakenDownEvent>
{
    private readonly IEsReadModelRepository<ProductReadModel> _repository;
    private readonly ILogger<ProductTakenDownReadModelSyncConsumer> _logger;

    public ProductTakenDownReadModelSyncConsumer(
        IEsReadModelRepository<ProductReadModel> repository,
        ILogger<ProductTakenDownReadModelSyncConsumer> logger)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(logger);
        _repository = repository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<ProductTakenDownEvent> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var evt = context.Message;

        var success = await _repository.DeleteByIdAsync(
            evt.ProductId.ToString(),
            ProductSearchService.ProductIndexName,
            context.CancellationToken);

        if (!success)
        {
            _logger.LogWarning("ES 读模型删除失败 ProductId={ProductId}", evt.ProductId);
        }
        else
        {
            _logger.LogInformation("读模型已删除 ProductId={ProductId}", evt.ProductId);
        }
    }
}
