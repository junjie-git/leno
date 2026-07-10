using Leno.SellerShop.Domain.ValueObjects;

namespace Leno.SellerShop.Application.DTOs;

/// <summary>
/// 店铺信息 DTO，返回店铺当前状态与基础资料。
/// </summary>
public sealed class ShopDto
{
    public Guid Id { get; init; }

    public Guid SellerId { get; init; }

    public string ShopName { get; init; } = string.Empty;

    public string? Logo { get; init; }

    public string? Description { get; init; }

    public string ContactPhone { get; init; } = string.Empty;

    public string? ContactEmail { get; init; }

    public string? BusinessLicenseNo { get; init; }

    public string? Address { get; init; }

    public ShopStatus Status { get; init; }

    public int ProductCount { get; init; }

    public string? StatusReason { get; init; }

    public Guid? ReviewedBy { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime UpdatedAt { get; init; }
}
