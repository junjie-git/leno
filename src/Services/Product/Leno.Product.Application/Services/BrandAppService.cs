using FluentValidation;
using Leno.Product.Application.DTOs;
using Leno.Product.Application.Exceptions;
using Leno.Product.Domain.Aggregates;
using Leno.Product.Domain.Exceptions;
using Leno.Product.Domain.Repositories;
using Leno.Product.Domain.ValueObjects;
using Leno.SharedContracts.Responses;
using Leno.SharedKernel.Abstractions;

namespace Leno.Product.Application.Services;

/// <summary>
/// 品牌管理应用服务实现，编排品牌 CRUD 与启停用例。
/// </summary>
public sealed class BrandAppService : IBrandAppService
{
    private readonly IBrandRepository _brandRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<CreateBrandDto> _createValidator;
    private readonly IValidator<UpdateBrandDto> _updateValidator;

    public BrandAppService(
        IBrandRepository brandRepository,
        IUnitOfWork unitOfWork,
        IValidator<CreateBrandDto> createValidator,
        IValidator<UpdateBrandDto> updateValidator)
    {
        _brandRepository = brandRepository;
        _unitOfWork = unitOfWork;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    /// <inheritdoc />
    public async Task<BrandDto> CreateAsync(CreateBrandDto dto, CancellationToken ct = default)
    {
        await ValidateAsync(_createValidator, dto, ct);
        var brand = Brand.Create(Guid.NewGuid(), dto.Name, dto.Logo);
        await _brandRepository.AddAsync(brand, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
        return ToBrandDto(brand);
    }

    /// <inheritdoc />
    public async Task<BrandDto> UpdateAsync(Guid brandId, UpdateBrandDto dto, CancellationToken ct = default)
    {
        await ValidateAsync(_updateValidator, dto, ct);
        var brand = await RequireBrandAsync(brandId, ct);
        brand.Update(dto.Name, dto.Logo);
        await _brandRepository.UpdateAsync(brand, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
        return ToBrandDto(brand);
    }

    /// <inheritdoc />
    public async Task EnableAsync(Guid brandId, CancellationToken ct = default)
    {
        var brand = await RequireBrandAsync(brandId, ct);
        brand.Enable();
        await _brandRepository.UpdateAsync(brand, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    /// <inheritdoc />
    public async Task DisableAsync(Guid brandId, CancellationToken ct = default)
    {
        var brand = await RequireBrandAsync(brandId, ct);
        brand.Disable();
        await _brandRepository.UpdateAsync(brand, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<PageResult<BrandDto>> QueryAsync(BrandQueryDto query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        BrandStatus? status = null;
        if (!string.IsNullOrWhiteSpace(query.Status)
            && Enum.TryParse<BrandStatus>(query.Status, ignoreCase: true, out var statusValue))
        {
            status = statusValue;
        }

        var (items, total) = await _brandRepository.QueryAsync(status, query.Keyword, query.Page, query.PageSize, ct);
        var dtos = items.Select(ToBrandDto).ToList();

        var safePage = query.Page < 1 ? 1 : query.Page;
        var safePageSize = query.PageSize is <= 0 or > 100 ? 20 : query.PageSize;

        return new PageResult<BrandDto>(dtos, total, safePage, safePageSize);
    }

    /// <inheritdoc />
    public async Task<BrandDto> GetByIdAsync(Guid brandId, CancellationToken ct = default)
    {
        var brand = await RequireBrandAsync(brandId, ct);
        return ToBrandDto(brand);
    }

    private async Task<Brand> RequireBrandAsync(Guid brandId, CancellationToken ct)
    {
        var brand = await _brandRepository.GetByIdAsync(brandId, ct);
        if (brand is null)
        {
            throw new ProductDomainException("品牌不存在", "BRAND_NOT_FOUND");
        }

        return brand;
    }

    private static async Task ValidateAsync<T>(IValidator<T> validator, T instance, CancellationToken ct)
    {
        var result = await validator.ValidateAsync(instance, ct);
        if (!result.IsValid)
        {
            throw new ProductValidationException(result.Errors.Select(e => e.ErrorMessage));
        }
    }

    private static BrandDto ToBrandDto(Brand brand)
        => new()
        {
            Id = brand.Id,
            Name = brand.Name,
            Logo = brand.Logo,
            Status = brand.Status,
            CreatedAt = brand.CreatedAt,
            UpdatedAt = brand.UpdatedAt
        };
}
