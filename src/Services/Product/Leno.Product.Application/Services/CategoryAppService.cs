using FluentValidation;
using Leno.Product.Application.DTOs;
using Leno.Product.Application.Exceptions;
using Leno.Product.Domain.Aggregates;
using Leno.Product.Domain.Exceptions;
using Leno.Product.Domain.Repositories;
using Leno.SharedKernel.Abstractions;

namespace Leno.Product.Application.Services;

/// <summary>
/// 分类管理应用服务实现，编排分类树管理与查询用例。
/// </summary>
public sealed class CategoryAppService : ICategoryAppService
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<CreateCategoryDto> _createValidator;
    private readonly IValidator<UpdateCategoryDto> _updateValidator;

    public CategoryAppService(
        ICategoryRepository categoryRepository,
        IUnitOfWork unitOfWork,
        IValidator<CreateCategoryDto> createValidator,
        IValidator<UpdateCategoryDto> updateValidator)
    {
        _categoryRepository = categoryRepository;
        _unitOfWork = unitOfWork;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    /// <inheritdoc />
    public async Task<CategoryDto> CreateAsync(CreateCategoryDto dto, CancellationToken ct = default)
    {
        await ValidateAsync(_createValidator, dto, ct);

        int? parentLevel = null;
        if (dto.ParentId.HasValue && dto.ParentId.Value != Guid.Empty)
        {
            var parent = await _categoryRepository.GetByIdAsync(dto.ParentId.Value, ct);
            if (parent is null)
            {
                throw new ProductDomainException("父分类不存在", "CATEGORY_PARENT_NOT_FOUND");
            }

            parentLevel = parent.Level;
        }

        if (await _categoryRepository.ExistsByNameAsync(dto.Name, dto.ParentId, ct))
        {
            throw new ProductDomainException("同级分类名称已存在", "CATEGORY_NAME_DUPLICATE");
        }

        var category = Category.Create(Guid.NewGuid(), dto.Name, dto.ParentId, parentLevel, dto.SortOrder);
        await _categoryRepository.AddAsync(category, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        return ToCategoryDto(category);
    }

    /// <inheritdoc />
    public async Task<CategoryDto> UpdateAsync(Guid categoryId, UpdateCategoryDto dto, CancellationToken ct = default)
    {
        await ValidateAsync(_updateValidator, dto, ct);
        var category = await RequireCategoryAsync(categoryId, ct);

        category.Update(dto.Name, dto.SortOrder);
        await _categoryRepository.UpdateAsync(category, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        return ToCategoryDto(category);
    }

    /// <inheritdoc />
    public async Task EnableAsync(Guid categoryId, CancellationToken ct = default)
    {
        var category = await RequireCategoryAsync(categoryId, ct);
        category.Enable();
        await _categoryRepository.UpdateAsync(category, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    /// <inheritdoc />
    public async Task DisableAsync(Guid categoryId, CancellationToken ct = default)
    {
        var category = await RequireCategoryAsync(categoryId, ct);
        category.Disable();
        await _categoryRepository.UpdateAsync(category, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CategoryDto>> GetTreeAsync(CancellationToken ct = default)
    {
        var all = await _categoryRepository.GetTreeAsync(ct);
        var lookup = all.ToLookup(c => c.ParentId);
        var roots = all.Where(c => c.ParentId is null)
            .OrderBy(c => c.SortOrder)
            .Select(c => ToCategoryDto(c, lookup))
            .ToList();

        return roots;
    }

    /// <inheritdoc />
    public async Task<CategoryDto> GetByIdAsync(Guid categoryId, CancellationToken ct = default)
    {
        var category = await RequireCategoryAsync(categoryId, ct);
        return ToCategoryDto(category);
    }

    private async Task<Category> RequireCategoryAsync(Guid categoryId, CancellationToken ct)
    {
        var category = await _categoryRepository.GetByIdAsync(categoryId, ct);
        if (category is null)
        {
            throw new ProductDomainException("分类不存在", "CATEGORY_NOT_FOUND");
        }

        return category;
    }

    private static async Task ValidateAsync<T>(IValidator<T> validator, T instance, CancellationToken ct)
    {
        var result = await validator.ValidateAsync(instance, ct);
        if (!result.IsValid)
        {
            throw new ProductValidationException(result.Errors.Select(e => e.ErrorMessage));
        }
    }

    private static CategoryDto ToCategoryDto(Category category)
        => new()
        {
            Id = category.Id,
            Name = category.Name,
            ParentId = category.ParentId,
            Level = category.Level,
            SortOrder = category.SortOrder,
            Status = category.Status
        };

    private static CategoryDto ToCategoryDto(Category category, ILookup<Guid?, Category> lookup)
        => new()
        {
            Id = category.Id,
            Name = category.Name,
            ParentId = category.ParentId,
            Level = category.Level,
            SortOrder = category.SortOrder,
            Status = category.Status,
            Children = lookup[category.Id]
                .OrderBy(c => c.SortOrder)
                .Select(c => ToCategoryDto(c, lookup))
                .ToList()
        };
}
