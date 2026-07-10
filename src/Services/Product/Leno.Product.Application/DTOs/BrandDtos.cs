using Leno.Product.Domain.ValueObjects;

namespace Leno.Product.Application.DTOs;

/// <summary>
/// 品牌详情 DTO。
/// </summary>
public sealed class BrandDto
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? Logo { get; init; }

    public BrandStatus Status { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime UpdatedAt { get; init; }
}

/// <summary>
/// 创建品牌 DTO。
/// </summary>
public sealed class CreateBrandDto
{
    public string Name { get; init; } = string.Empty;

    public string? Logo { get; init; }
}

/// <summary>
/// 更新品牌 DTO。
/// </summary>
public sealed class UpdateBrandDto
{
    public string Name { get; init; } = string.Empty;

    public string? Logo { get; init; }
}
