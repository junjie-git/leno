using Leno.SellerShop.Domain.ValueObjects;

namespace Leno.SellerShop.Application.DTOs;

/// <summary>
/// 卖家档案 DTO，返回卖家实名与资质信息。
/// </summary>
public sealed class SellerProfileDto
{
    public Guid Id { get; init; }

    public Guid UserId { get; init; }

    public string RealName { get; init; } = string.Empty;

    public string? IdCard { get; init; }

    public string? BusinessLicenseNo { get; init; }

    public string? BankAccount { get; init; }

    public SellerStatus Status { get; init; }

    public Guid? ReviewedBy { get; init; }

    public string? StatusReason { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime UpdatedAt { get; init; }
}
