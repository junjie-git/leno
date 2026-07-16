using Leno.Infrastructure.EventBus;
using Leno.Infrastructure.ReadModel;
using Leno.Product.Domain.Repositories;
using Leno.Product.Infrastructure.ReadModels;
using Leno.SharedContracts.Events;
using Microsoft.Extensions.Logging;
using Leno.Infrastructure.Abstractions;

namespace Leno.Product.Infrastructure.Consumers;

/// <summary>
/// 库存调整事件消费者：消费 <see cref="StockAdjustedEvent"/>，
/// 加载对应 SPU 聚合并更新 ES 读模型中的价格区间。
/// 幂等：ES 索引以商品标识为 _id，重复索引为覆盖更新。
/// </summary>
public sealed class StockAdjustedEventConsumer : IntegrationEventConsumerBase<StockAdjustedEvent>
{
    private readonly ISPURepository _spuRepository;
    private readonly IEsReadModelRepository<ProductReadModel> _repository;

    public StockAdjustedEventConsumer(
        ISPURepository spuRepository,
        IEsReadModelRepository<ProductReadModel> repository,
        ILogger<StockAdjustedEventConsumer> logger,
        IIdempotencyStore idempotencyStore)
        : base(logger, idempotencyStore)
    {
        ArgumentNullException.ThrowIfNull(spuRepository);
        ArgumentNullException.ThrowIfNull(repository);
        _spuRepository = spuRepository;
        _repository = repository;
    }

    /// <inheritdoc />
    protected override async Task HandleAsync(StockAdjustedEvent integrationEvent, CancellationToken ct)
    {
        var spu = await _spuRepository.GetByIdAsync(integrationEvent.ProductId, ct);
        if (spu is null)
        {
            Logger.LogWarning("商品不存在，跳过读模型同步 ProductId={ProductId}", integrationEvent.ProductId);
            return;
        }

        var existing = await _repository.GetByIdAsync(
            spu.Id.ToString(),
            ProductSearchService.ProductIndexName,
            ct);

        if (existing is null)
        {
            Logger.LogInformation("ES 读模型不存在，跳过更新 ProductId={ProductId}", spu.Id);
            return;
        }

        var prices = spu.SKUs.Select(s => s.Price.Amount).ToList();
        var minPrice = prices.Count != 0 ? prices.Min() : existing.MinPrice;
        var maxPrice = prices.Count != 0 ? prices.Max() : existing.MaxPrice;

        var updated = new ProductReadModel
        {
            Id = existing.Id,
            Title = existing.Title,
            Subtitle = existing.Subtitle,
            MainImageUrl = existing.MainImageUrl,
            CategoryId = existing.CategoryId,
            BrandId = existing.BrandId,
            ShopId = existing.ShopId,
            Status = existing.Status,
            Specs = existing.Specs,
            MinPrice = minPrice,
            MaxPrice = maxPrice,
            Currency = existing.Currency,
            IndexedAt = DateTime.UtcNow
        };

        await _repository.IndexAsync(
            updated,
            spu.Id.ToString(),
            ProductSearchService.ProductIndexName,
            ct);

        Logger.LogInformation("读模型已更新 ProductId={ProductId}", spu.Id);
    }
}