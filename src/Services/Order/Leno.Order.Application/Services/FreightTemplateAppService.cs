using Leno.Order.Application.DTOs;
using Leno.Order.Domain.Aggregates;
using Leno.Order.Domain.Exceptions;
using Leno.Order.Domain.Repositories;
using Leno.Order.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.Order.Application.Services;

/// <summary>
/// 运费模板应用服务实现，编排卖家运费模板 CRUD、区域规则更新、启停与查询用例。
/// </summary>
public sealed class FreightTemplateAppService : IFreightTemplateAppService
{
    private readonly IFreightTemplateRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public FreightTemplateAppService(IFreightTemplateRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task<FreightTemplateDto> CreateAsync(CreateFreightTemplateDto dto, CancellationToken ct = default)
    {
        var entity = FreightTemplate.Create(
            Guid.NewGuid(), dto.SellerId, dto.Name, dto.Type, dto.FreeShippingThreshold);
        var rules = dto.RegionRules.Select(ToRule).ToList();
        entity.UpdateRules(rules);
        await _repository.AddAsync(entity, ct).ConfigureAwait(false);
        await _unitOfWork.SaveEntitiesAsync(ct).ConfigureAwait(false);
        return ToDto(entity);
    }

    /// <inheritdoc />
    public async Task<FreightTemplateDto> UpdateRulesAsync(Guid id, UpdateFreightTemplateRulesDto dto, CancellationToken ct = default)
    {
        var entity = await RequireAsync(id, ct).ConfigureAwait(false);
        var rules = dto.RegionRules.Select(ToRule).ToList();
        entity.UpdateRules(rules);
        await _unitOfWork.SaveEntitiesAsync(ct).ConfigureAwait(false);
        return ToDto(entity);
    }

    /// <inheritdoc />
    public async Task EnableAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await RequireAsync(id, ct).ConfigureAwait(false);
        entity.Enable();
        await _unitOfWork.SaveEntitiesAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DisableAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await RequireAsync(id, ct).ConfigureAwait(false);
        entity.Disable();
        await _unitOfWork.SaveEntitiesAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<FreightTemplateDto?> GetBySellerIdAsync(Guid sellerId, CancellationToken ct = default)
    {
        var entity = await _repository.GetBySellerIdAsync(sellerId, ct).ConfigureAwait(false);
        return entity is null ? null : ToDto(entity);
    }

    /// <inheritdoc />
    public async Task<List<FreightTemplateDto>> ListAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var list = await _repository.ListAsync(page, pageSize, ct).ConfigureAwait(false);
        return list.Select(ToDto).ToList();
    }

    private async Task<FreightTemplate> RequireAsync(Guid id, CancellationToken ct)
        => (await _repository.GetByIdAsync(id, ct).ConfigureAwait(false))
           ?? throw new OrderDomainException($"运费模板 {id} 不存在", "FREIGHT_NOT_FOUND");

    private static FreightRegionRule ToRule(FreightRegionRuleDto dto)
        => FreightRegionRule.Create(
            dto.RegionCode, dto.FirstUnit, dto.FirstPrice, dto.AdditionalUnit, dto.AdditionalPrice);

    private static FreightTemplateDto ToDto(FreightTemplate entity)
        => new()
        {
            Id = entity.Id,
            SellerId = entity.SellerId,
            Name = entity.Name,
            Type = entity.Type,
            FreeShippingThreshold = entity.FreeShippingThreshold,
            Status = entity.Status,
            RegionRules = entity.RegionRules.Select(ToRuleDto).ToList()
        };

    private static FreightRegionRuleDto ToRuleDto(FreightRegionRule rule)
        => new()
        {
            RegionCode = rule.RegionCode,
            FirstUnit = rule.FirstUnit,
            FirstPrice = rule.FirstPrice,
            AdditionalUnit = rule.AdditionalUnit,
            AdditionalPrice = rule.AdditionalPrice
        };
}
