using Leno.SellerShop.Domain.ValueObjects;

namespace Leno.SellerShop.Application.DTOs;

/// <summary>
/// 店铺资质 DTO，返回资质详情与审核状态。
/// </summary>
public sealed class QualificationDto
{
    public Guid Id { get; init; }

    public Guid ShopId { get; init; }

    public QualificationType Type { get; init; }

    public string Number { get; init; } = string.Empty;

    public string ImageUrl { get; init; } = string.Empty;

    public DateTime ValidFrom { get; init; }

    public DateTime ValidTo { get; init; }

    public QualificationStatus Status { get; init; }

    public string? RejectReason { get; init; }

    public Guid? ReviewedBy { get; init; }

    public DateTime CreatedAt { get; init; }
}