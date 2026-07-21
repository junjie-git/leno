using FluentValidation;
using Leno.Product.Application.DTOs;
using Leno.Product.Application.Exceptions;
using Leno.Product.Domain.Aggregates;
using Leno.Product.Domain.Exceptions;
using Leno.Product.Domain.Repositories;
using Leno.SharedKernel.Abstractions;

namespace Leno.Product.Application.Services;

/// <summary>
/// 库存管理应用服务实现，编排卖家/运营补货用例。
/// 不存在库存基线时按补货量初始化；存在时调用聚合并发布 <c>StockAdjustedEvent</c>。
/// </summary>
public sealed class InventoryAppService : IInventoryAppService
{
    private readonly IStockBaselineRepository _stockBaselineRepository;
    private readonly ISPURepository _spuRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<ReplenishStockDto> _replenishValidator;

    public InventoryAppService(
        IStockBaselineRepository stockBaselineRepository,
        ISPURepository spuRepository,
        IUnitOfWork unitOfWork,
        IValidator<ReplenishStockDto> replenishValidator)
    {
        _stockBaselineRepository = stockBaselineRepository;
        _spuRepository = spuRepository;
        _unitOfWork = unitOfWork;
        _replenishValidator = replenishValidator;
    }

    /// <inheritdoc />
    public async Task ReplenishAsync(Guid skuId, ReplenishStockDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await ValidateAsync(_replenishValidator, dto, ct);

        if (skuId == Guid.Empty)
        {
            throw new ProductDomainException("SKU 标识不可为空", "STOCK_SKU_EMPTY");
        }

        var spu = await _spuRepository.GetBySkuIdAsync(skuId, ct);
        if (spu is null)
        {
            throw new ProductDomainException("SKU 不存在", "SPU_SKU_NOT_FOUND");
        }

        var baseline = await _stockBaselineRepository.GetBySkuIdAsync(skuId, ct);
        if (baseline is null)
        {
            baseline = StockBaseline.Create(Guid.NewGuid(), skuId, dto.Quantity, spu.Id);
            await _stockBaselineRepository.AddAsync(baseline, ct);
        }
        else
        {
            baseline.Replenish(dto.Quantity);
            await _stockBaselineRepository.UpdateAsync(baseline, ct);
        }

        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    private static async Task ValidateAsync<T>(IValidator<T> validator, T instance, CancellationToken ct)
    {
        var result = await validator.ValidateAsync(instance, ct);
        if (!result.IsValid)
        {
            throw new ProductValidationException(result.Errors.Select(e => e.ErrorMessage));
        }
    }
}
