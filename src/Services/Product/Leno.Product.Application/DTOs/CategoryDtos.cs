using Leno.Product.Domain.ValueObjects;

namespace Leno.Product.Application.DTOs;

/// <summary>
/// 分类详情 DTO。
/// </summary>
public sealed class CategoryDto
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public Guid? ParentId { get; init; }

    public int Level { get; init; }

    public int SortOrder { get; init; }

    public CategoryStatus Status { get; init; }

    public IReadOnlyList<CategoryDto> Children { get; init; } = Array.Empty<CategoryDto>();
}

/// <summary>
/// 创建分类 DTO。
/// </summary>
public sealed class CreateCategoryDto
{
    public string Name { get; init; } = string.Empty;

    public Guid? ParentId { get; init; }

    public int SortOrder { get; init; }
}

/// <summary>
/// 更新分类 DTO。
/// </summary>
public sealed class UpdateCategoryDto
{
    public string Name { get; init; } = string.Empty;

    public int SortOrder { get; init; }
}
