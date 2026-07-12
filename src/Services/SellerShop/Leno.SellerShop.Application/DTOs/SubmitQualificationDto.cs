using Leno.SellerShop.Domain.ValueObjects;

namespace Leno.SellerShop.Application.DTOs;

/// <summary>
/// 提交资质 DTO，用于卖家上传店铺经营资质。
/// </summary>
public sealed class SubmitQualificationDto
{
    /// <summary>资质类型。</summary>
    public QualificationType Type { get; init; }

    /// <summary>资质编号（如营业执照号）。</summary>
    public string Number { get; init; } = string.Empty;

    /// <summary>有效期起始（UTC）。</summary>
    public DateTime ValidFrom { get; init; }

    /// <summary>有效期截止（UTC）。</summary>
    public DateTime ValidTo { get; init; }
}