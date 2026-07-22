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

    /// <summary>
    /// 幂等键（可选），由客户端生成的 UUID。
    /// 相同 IdempotencyKey 的重复提交将被识别并跳过，避免网络重试导致重复创建资质。
    /// 未提供时按普通流程处理，不做幂等保护。
    /// </summary>
    public Guid? IdempotencyKey { get; init; }
}