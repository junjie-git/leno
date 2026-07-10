using Leno.SharedKernel.Abstractions;
using Leno.SellerShop.Domain.Exceptions;
using Leno.SellerShop.Domain.ValueObjects;

namespace Leno.SellerShop.Domain.Aggregates;

/// <summary>
/// 卖家档案聚合根，承载卖家实名信息与资质材料，与店铺一对一关联。
/// 卖家账号认证细节归属用户域，本聚合只持有 UserId 引用与经营资质。
/// </summary>
public sealed class SellerProfile : AggregateRoot
{
    private const int MaxRealNameLength = 32;
    private const int MaxIdCardLength = 18;
    private const int MaxBankAccountLength = 64;
    private const int MaxBusinessLicenseNoLength = 32;

    /// <summary>卖家账号标识（引用用户域 UserId）。</summary>
    public Guid UserId { get; private set; }

    /// <summary>真实姓名。</summary>
    public string RealName { get; private set; } = string.Empty;

    /// <summary>身份证号，可空（企业卖家可无）。</summary>
    public string? IdCard { get; private set; }

    /// <summary>营业执照号，可空（个人卖家可无）。</summary>
    public string? BusinessLicenseNo { get; private set; }

    /// <summary>收款银行账号，可空。</summary>
    public string? BankAccount { get; private set; }

    /// <summary>卖家档案状态。</summary>
    public SellerStatus Status { get; private set; }

    /// <summary>审核人标识（通过/驳回时记录）。</summary>
    public Guid? ReviewedBy { get; private set; }

    /// <summary>状态变更原因（驳回时记录）。</summary>
    public string? StatusReason { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private SellerProfile() { }

    private SellerProfile(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，创建处于草稿态的卖家档案。
    /// </summary>
    /// <param name="sellerId">卖家档案标识，由应用层生成。</param>
    /// <param name="userId">卖家账号标识（用户域 UserId）。</param>
    /// <param name="realName">真实姓名。</param>
    /// <param name="idCard">身份证号，可空。</param>
    /// <param name="businessLicenseNo">营业执照号，可空。</param>
    /// <param name="bankAccount">收款银行账号，可空。</param>
    public static SellerProfile Create(
        Guid sellerId,
        Guid userId,
        string realName,
        string? idCard = null,
        string? businessLicenseNo = null,
        string? bankAccount = null)
    {
        if (sellerId == Guid.Empty)
        {
            throw new SellerShopDomainException("卖家档案标识不可为空", "SELLER_ID_EMPTY");
        }

        if (userId == Guid.Empty)
        {
            throw new SellerShopDomainException("卖家账号标识不可为空", "SELLER_USER_EMPTY");
        }

        ValidateRealName(realName);
        ValidateIdCard(idCard);
        ValidateBusinessLicenseNo(businessLicenseNo);
        ValidateBankAccount(bankAccount);

        return new SellerProfile(sellerId)
        {
            UserId = userId,
            RealName = realName.Trim(),
            IdCard = string.IsNullOrWhiteSpace(idCard) ? null : idCard.Trim(),
            BusinessLicenseNo = string.IsNullOrWhiteSpace(businessLicenseNo) ? null : businessLicenseNo.Trim(),
            BankAccount = string.IsNullOrWhiteSpace(bankAccount) ? null : bankAccount.Trim(),
            Status = SellerStatus.Draft
        };
    }

    /// <summary>
    /// 更新卖家档案可变字段（实名信息与资质）。
    /// </summary>
    public void Update(
        string realName,
        string? idCard = null,
        string? businessLicenseNo = null,
        string? bankAccount = null)
    {
        if (Status == SellerStatus.Approved)
        {
            throw new SellerShopDomainException("已通过的卖家档案不可直接修改，须重新提交审核", "SELLER_APPROVED", 409);
        }

        ValidateRealName(realName);
        ValidateIdCard(idCard);
        ValidateBusinessLicenseNo(businessLicenseNo);
        ValidateBankAccount(bankAccount);

        RealName = realName.Trim();
        IdCard = string.IsNullOrWhiteSpace(idCard) ? null : idCard.Trim();
        BusinessLicenseNo = string.IsNullOrWhiteSpace(businessLicenseNo) ? null : businessLicenseNo.Trim();
        BankAccount = string.IsNullOrWhiteSpace(bankAccount) ? null : bankAccount.Trim();
    }

    /// <summary>
    /// 提交审核，仅草稿/已驳回态可调用，流转至待审核。
    /// </summary>
    public void SubmitForVerification()
    {
        if (Status != SellerStatus.Draft && Status != SellerStatus.Rejected)
        {
            throw new SellerShopDomainException(
                $"当前状态为 {Status}，不可提交审核", "SELLER_INVALID_TRANSITION", 409);
        }

        if (string.IsNullOrWhiteSpace(BusinessLicenseNo) && string.IsNullOrWhiteSpace(IdCard))
        {
            throw new SellerShopDomainException("须提供营业执照号或身份证号之一", "SELLER_NO_QUALIFICATION");
        }

        Status = SellerStatus.PendingReview;
        StatusReason = null;
    }

    /// <summary>
    /// 审核通过，仅待审核态可调用，流转至已通过。
    /// </summary>
    public void Approve(Guid reviewedBy)
    {
        if (Status != SellerStatus.PendingReview)
        {
            throw new SellerShopDomainException(
                $"当前状态为 {Status}，不可审核通过", "SELLER_INVALID_TRANSITION", 409);
        }

        if (reviewedBy == Guid.Empty)
        {
            throw new SellerShopDomainException("审核人标识不可为空", "SELLER_REVIEWER_EMPTY");
        }

        Status = SellerStatus.Approved;
        ReviewedBy = reviewedBy;
        StatusReason = null;
    }

    /// <summary>
    /// 审核驳回，仅待审核态可调用，流转至已驳回。
    /// </summary>
    public void Reject(Guid reviewedBy, string reason)
    {
        if (Status != SellerStatus.PendingReview)
        {
            throw new SellerShopDomainException(
                $"当前状态为 {Status}，不可驳回", "SELLER_INVALID_TRANSITION", 409);
        }

        if (reviewedBy == Guid.Empty)
        {
            throw new SellerShopDomainException("审核人标识不可为空", "SELLER_REVIEWER_EMPTY");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new SellerShopDomainException("驳回原因不可为空", "SELLER_REASON_EMPTY");
        }

        Status = SellerStatus.Rejected;
        ReviewedBy = reviewedBy;
        StatusReason = reason.Trim();
    }

    private static void ValidateRealName(string realName)
    {
        if (string.IsNullOrWhiteSpace(realName))
        {
            throw new SellerShopDomainException("真实姓名不可为空", "SELLER_REAL_NAME_EMPTY");
        }

        if (realName.Trim().Length > MaxRealNameLength)
        {
            throw new SellerShopDomainException(
                $"真实姓名长度不可超过 {MaxRealNameLength} 字符", "SELLER_REAL_NAME_LENGTH");
        }
    }

    private static void ValidateIdCard(string? idCard)
    {
        if (!string.IsNullOrWhiteSpace(idCard) && idCard.Trim().Length > MaxIdCardLength)
        {
            throw new SellerShopDomainException(
                $"身份证号长度不可超过 {MaxIdCardLength} 字符", "SELLER_ID_CARD_LENGTH");
        }
    }

    private static void ValidateBusinessLicenseNo(string? licenseNo)
    {
        if (!string.IsNullOrWhiteSpace(licenseNo) && licenseNo.Trim().Length > MaxBusinessLicenseNoLength)
        {
            throw new SellerShopDomainException(
                $"营业执照号长度不可超过 {MaxBusinessLicenseNoLength} 字符", "SELLER_LICENSE_LENGTH");
        }
    }

    private static void ValidateBankAccount(string? bankAccount)
    {
        if (!string.IsNullOrWhiteSpace(bankAccount) && bankAccount.Trim().Length > MaxBankAccountLength)
        {
            throw new SellerShopDomainException(
                $"收款银行账号长度不可超过 {MaxBankAccountLength} 字符", "SELLER_BANK_ACCOUNT_LENGTH");
        }
    }
}
