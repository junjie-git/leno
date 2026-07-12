using Leno.SharedKernel.Abstractions;
using Leno.SellerShop.Domain.Exceptions;
using Leno.SellerShop.Domain.ValueObjects;

namespace Leno.SellerShop.Domain.Entities;

/// <summary>
/// 店铺资质实体，属于 Shop 聚合的内部实体，承载资质类型、编号、图片 URL 与有效期。
/// 生命周期由 Shop 聚合根管理，不可独立于 Shop 存在。
/// </summary>
public sealed class ShopQualification : Entity
{
    private const int MaxNumberLength = 64;
    private const int MaxImageUrlLength = 512;
    private const int MaxRejectReasonLength = 200;

    /// <summary>所属店铺标识。</summary>
    public Guid ShopId { get; private set; }

    /// <summary>资质类型。</summary>
    public QualificationType Type { get; private set; }

    /// <summary>资质编号（如营业执照号）。</summary>
    public string Number { get; private set; } = string.Empty;

    /// <summary>资质图片 URL。</summary>
    public string ImageUrl { get; private set; } = string.Empty;

    /// <summary>有效期起始（UTC）。</summary>
    public DateTime ValidFrom { get; private set; }

    /// <summary>有效期截止（UTC）。</summary>
    public DateTime ValidTo { get; private set; }

    /// <summary>审核状态。</summary>
    public QualificationStatus Status { get; private set; }

    /// <summary>驳回原因，可空。</summary>
    public string? RejectReason { get; private set; }

    /// <summary>审核人标识。</summary>
    public Guid? ReviewedBy { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private ShopQualification() { }

    private ShopQualification(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，创建待审核状态的店铺资质。
    /// </summary>
    /// <param name="qualificationId">资质标识，由调用方生成。</param>
    /// <param name="shopId">所属店铺标识。</param>
    /// <param name="type">资质类型。</param>
    /// <param name="number">资质编号。</param>
    /// <param name="imageUrl">资质图片 URL。</param>
    /// <param name="validFrom">有效期起始。</param>
    /// <param name="validTo">有效期截止。</param>
    public static ShopQualification Create(
        Guid qualificationId,
        Guid shopId,
        QualificationType type,
        string number,
        string imageUrl,
        DateTime validFrom,
        DateTime validTo)
    {
        if (qualificationId == Guid.Empty)
        {
            throw new SellerShopDomainException("资质标识不可为空", "QUALIFICATION_ID_EMPTY");
        }

        if (shopId == Guid.Empty)
        {
            throw new SellerShopDomainException("店铺标识不可为空", "QUALIFICATION_SHOP_EMPTY");
        }

        if (string.IsNullOrWhiteSpace(number))
        {
            throw new SellerShopDomainException("资质编号不可为空", "QUALIFICATION_NUMBER_EMPTY");
        }

        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            throw new SellerShopDomainException("资质图片 URL 不可为空", "QUALIFICATION_IMAGE_EMPTY");
        }

        if (number.Trim().Length > MaxNumberLength)
        {
            throw new SellerShopDomainException(
                $"资质编号长度不可超过 {MaxNumberLength} 字符", "QUALIFICATION_NUMBER_LENGTH");
        }

        if (imageUrl.Trim().Length > MaxImageUrlLength)
        {
            throw new SellerShopDomainException(
                $"资质图片 URL 长度不可超过 {MaxImageUrlLength} 字符", "QUALIFICATION_IMAGE_LENGTH");
        }

        if (validFrom >= validTo)
        {
            throw new SellerShopDomainException("有效期起始须早于截止日期", "QUALIFICATION_DATE_INVALID");
        }

        return new ShopQualification(qualificationId)
        {
            ShopId = shopId,
            Type = type,
            Number = number.Trim(),
            ImageUrl = imageUrl.Trim(),
            ValidFrom = validFrom,
            ValidTo = validTo,
            Status = QualificationStatus.Pending
        };
    }

    /// <summary>
    /// 审核通过，仅待审核态可调用。
    /// </summary>
    /// <param name="reviewedBy">审核人标识。</param>
    public void Approve(Guid reviewedBy)
    {
        if (Status != QualificationStatus.Pending)
        {
            throw new SellerShopDomainException(
                $"当前资质状态为 {Status}，不可审核通过", "QUALIFICATION_INVALID_TRANSITION", 409);
        }

        if (reviewedBy == Guid.Empty)
        {
            throw new SellerShopDomainException("审核人标识不可为空", "QUALIFICATION_REVIEWER_EMPTY");
        }

        Status = QualificationStatus.Approved;
        ReviewedBy = reviewedBy;
        RejectReason = null;
    }

    /// <summary>
    /// 审核驳回，仅待审核态可调用。
    /// </summary>
    /// <param name="reviewedBy">审核人标识。</param>
    /// <param name="reason">驳回原因。</param>
    public void Reject(Guid reviewedBy, string reason)
    {
        if (Status != QualificationStatus.Pending)
        {
            throw new SellerShopDomainException(
                $"当前资质状态为 {Status}，不可驳回", "QUALIFICATION_INVALID_TRANSITION", 409);
        }

        if (reviewedBy == Guid.Empty)
        {
            throw new SellerShopDomainException("审核人标识不可为空", "QUALIFICATION_REVIEWER_EMPTY");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new SellerShopDomainException("驳回原因不可为空", "QUALIFICATION_REASON_EMPTY");
        }

        if (reason.Trim().Length > MaxRejectReasonLength)
        {
            throw new SellerShopDomainException(
                $"驳回原因长度不可超过 {MaxRejectReasonLength} 字符", "QUALIFICATION_REASON_LENGTH");
        }

        Status = QualificationStatus.Rejected;
        ReviewedBy = reviewedBy;
        RejectReason = reason.Trim();
    }

    /// <summary>
    /// 标记为过期，仅已通过态可调用，流转至已过期。
    /// </summary>
    public void MarkExpired()
    {
        if (Status != QualificationStatus.Approved)
        {
            throw new SellerShopDomainException(
                $"当前资质状态为 {Status}，不可标记过期", "QUALIFICATION_INVALID_TRANSITION", 409);
        }

        Status = QualificationStatus.Expired;
    }

    /// <summary>
    /// 判断在指定 UTC 时刻资质是否有效（已通过且未过期）。
    /// </summary>
    public bool IsValidAt(DateTime utcNow)
    {
        if (Status != QualificationStatus.Approved)
        {
            return false;
        }

        return ValidTo > utcNow;
    }

    /// <summary>
    /// 判断资质是否在指定日期前即将到期（withinDays 天内到期）。
    /// </summary>
    public bool IsExpiringWithin(int days, DateTime utcNow)
    {
        if (Status != QualificationStatus.Approved)
        {
            return false;
        }

        var remaining = ValidTo - utcNow;
        return remaining.TotalDays <= days && remaining.TotalDays > 0;
    }
}