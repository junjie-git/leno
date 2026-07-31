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
        if (skuIds.Count == 0)
        {
            return new List<SkuInfoResultDto>();
        }

        // 修复审计 #8：原实现遍历 skuIds 逐条调用 GetSkuInfoAsync（N+1 查询，100 个 SKU 触发 100 次 DB round-trip）。
        // 现改为单次批量查询 GetBySkuIdsAsync，内存中匹配 SKU 映射 DTO。
        var skuIdSet = skuIds.Distinct().ToHashSet();
        var spus = await _spuRepository.GetBySkuIdsAsync(skuIdSet, ct);

        var results = new List<SkuInfoResultDto>();
        foreach (var spu in spus)
        {
            foreach (var sku in spu.SKUs)
            {
                if (skuIdSet.Contains(sku.Id))
                {
                    results.Add(ToSkuInfoResultDto(spu, sku));
                }
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

    /// <inheritdoc />
    public async Task<List<LowStockSkuDto>> GetLowStockByShopAsync(Guid shopId, int threshold, CancellationToken ct = default)
    {
        if (shopId == Guid.Empty)
        {
            return new List<LowStockSkuDto>();
        }

        var spus = await _spuRepository.GetByShopIdAsync(shopId, ct)
            .ConfigureAwait(false);

        return spus
            .SelectMany(spu => spu.SKUs.Select(sku => new LowStockSkuDto
            {
                SkuId = sku.Id,
                ProductId = spu.Id,
                ProductName = spu.Title,
                SkuName = BuildSkuName(sku),
                Stock = sku.StockQty,
                Threshold = threshold,
                ShopId = shopId
            }))
            .Where(x => x.Stock < threshold)
            .OrderBy(x => x.Stock)
            .ToList();
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

    /// <summary>
    /// 拼接 SKU 规格名称：以 "/" 连接各规格属性的 Value（如 "红/XL"）。
    /// 无规格属性时回退为 SkuCode，保证 SkuName 永不为 null。
    /// </summary>
    private static string BuildSkuName(SKU sku)
    {
        var attrs = sku.SpecAttributes?.Attributes;
        if (attrs is { Count: > 0 })
        {
            return string.Join("/", attrs.Select(a => a.Value));
        }

        return sku.SkuCode ?? string.Empty;
    }
}
