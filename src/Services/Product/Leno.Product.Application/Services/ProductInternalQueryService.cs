using Leno.Product.Domain.Aggregates;
using Leno.Product.Domain.Repositories;
using Leno.Product.Domain.ValueObjects;

namespace Leno.Product.Application.Services;

/// <summary>
/// 商品域内部查询服务实现，基于 SPU 仓储查询 SKU 概要信息，供其他微服务跨域调用。
/// SKU 为 SPU 聚合内实体，通过 <see cref="ISPURepository.GetBySkuIdAsync"/> 定位其所属 SPU 后映射。
/// </summary>
public sealed class ProductInternalQueryService : IProductInternalQueryService
{
    private readonly ISPURepository _spuRepository;

    public ProductInternalQueryService(ISPURepository spuRepository)
    {
        ArgumentNullException.ThrowIfNull(spuRepository);
        _spuRepository = spuRepository;
    }

    /// <inheritdoc />
    public async Task<SkuInfoResultDto?> GetSkuInfoAsync(Guid skuId, CancellationToken ct = default)
    {
        if (skuId == Guid.Empty)
        {
            return null;
        }

        var spu = await _spuRepository.GetBySkuIdAsync(skuId, ct);
        if (spu is null)
        {
            return null;
        }

        var sku = spu.SKUs.FirstOrDefault(s => s.Id == skuId);
        if (sku is null)
        {
            return null;
        }

        return ToSkuInfoResultDto(spu, sku);
    }

    /// <inheritdoc />
    public async Task<List<SkuInfoResultDto>> GetSkuInfosBatchAsync(List<Guid> skuIds, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(skuIds);

        var results = new List<SkuInfoResultDto>();
        foreach (var skuId in skuIds)
        {
            var dto = await GetSkuInfoAsync(skuId, ct);
            if (dto is not null)
            {
                results.Add(dto);
            }
        }

        return results;
    }

    private static SkuInfoResultDto ToSkuInfoResultDto(SPU spu, SKU sku)
        => new()
        {
            SkuId = sku.Id,
            SpuId = spu.Id,
            Price = sku.Price.Amount,
            Currency = sku.Price.Currency,
            Available = sku.Status == SkuStatus.Active && sku.StockQty > 0,
            Stock = sku.StockQty,
            Status = sku.Status.ToString().ToLowerInvariant(),
            Title = spu.Title,
            MainImageUrl = spu.MainImageUrl,
            SellerId = spu.SellerId,
            ShopId = spu.ShopId,
            UpdatedAt = spu.UpdatedAt
        };
}
