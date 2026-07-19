using Leno.Product.Domain.Aggregates;
using Leno.Product.Domain.Repositories;
using Leno.Product.Domain.ValueObjects;
using Leno.SharedKernel.ValueObjects;

namespace Leno.Product.Application.Services;

/// <summary>
/// 商品域内部查询服务实现，基于 SPU 仓储查询 SKU 概要信息，供其他微服务跨域调用。
/// SKU 为 SPU 聚合内实体，通过 <see cref="ISPURepository.GetBySkuIdAsync"/> 定位其所属 SPU 后映射。
/// 库存查询通过 <see cref="IStockBaselineRepository"/> 直接读取 StockBaseline 聚合（与 SKU 概要查询解耦）。
/// </summary>
public sealed class ProductInternalQueryService : IProductInternalQueryService
{
    private readonly ISPURepository _spuRepository;
    private readonly IStockBaselineRepository _stockBaselineRepository;

    public ProductInternalQueryService(
        ISPURepository spuRepository,
        IStockBaselineRepository stockBaselineRepository)
    {
        ArgumentNullException.ThrowIfNull(spuRepository);
        ArgumentNullException.ThrowIfNull(stockBaselineRepository);
        _spuRepository = spuRepository;
        _stockBaselineRepository = stockBaselineRepository;
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

    /// <inheritdoc />
    public async Task<SkuStockResultDto?> GetSkuStockAsync(Guid skuId, CancellationToken ct = default)
    {
        if (skuId == Guid.Empty)
        {
            return null;
        }

        var baseline = await _stockBaselineRepository.GetBySkuIdAsync(skuId, ct)
            .ConfigureAwait(false);
        if (baseline is null)
        {
            return null;
        }

        return new SkuStockResultDto(skuId, baseline.AvailableQty, baseline.ReservedQty);
    }

    /// <inheritdoc />
    public async Task<SpuDetailResultDto?> GetSpuDetailAsync(Guid spuId, CancellationToken ct = default)
    {
        if (spuId == Guid.Empty)
        {
            return null;
        }

        var spu = await _spuRepository.GetByIdAsync(spuId, ct)
            .ConfigureAwait(false);
        if (spu is null)
        {
            return null;
        }

        var skus = spu.SKUs
            .Select(k => new SpuSkuDto(
                k.Id,
                k.SkuCode,
                spu.Title,
                k.ImageUrl ?? spu.MainImageUrl,
                k.Price.Amount,
                k.Price.Currency,
                k.StockQty,
                k.Status.ToString().ToLowerInvariant()))
            .ToList();

        return new SpuDetailResultDto(
            spu.Id,
            spu.SellerId,
            spu.ShopId,
            spu.Title,
            spu.Subtitle ?? string.Empty,
            spu.MainImageUrl,
            Description: string.Empty,
            skus);
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
