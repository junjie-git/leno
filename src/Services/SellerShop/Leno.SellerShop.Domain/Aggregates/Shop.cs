using Leno.SharedKernel.Abstractions;
using Leno.SellerShop.Domain.Events;
using Leno.SellerShop.Domain.Exceptions;
using Leno.SellerShop.Domain.ValueObjects;

namespace Leno.SellerShop.Domain.Aggregates;

/// <summary>
/// 店铺聚合根，封装店铺信息、状态机与商品计数不变量。
/// 一个卖家账号（SellerId，即用户域 UserId）对应一个店铺。
/// 所有状态流转通过行为意图明确的方法完成，禁止外部直接 set 字段。
/// </summary>
public sealed class Shop : AggregateRoot
{
    private const int MaxShopNameLength = 32;
    private const int MinShopNameLength = 2;
    private const int MaxDescriptionLength = 1000;
    private const int MaxPhoneLength = 20;
    private const int MaxEmailLength = 256;
    private const int MaxLogoLength = 512;
    private const int MaxBusinessLicenseNoLength = 32;
    private const int MaxAddressLength = 256;
    private const int MaxReasonLength = 200;

    /// <summary>卖家账号标识（引用用户域 UserId，一卖家一店铺）。</summary>
    public Guid SellerId { get; private set; }

    /// <summary>店铺名称，2-32 字，全局唯一。</summary>
    public string ShopName { get; private set; } = string.Empty;

    /// <summary>店铺 Logo URL，可空。</summary>
    public string? Logo { get; private set; }

    /// <summary>店铺描述，≤1000 字，可空。</summary>
    public string? Description { get; private set; }

    /// <summary>客服电话（E.164 或国内座机格式）。</summary>
    public string ContactPhone { get; private set; } = string.Empty;

    /// <summary>客服邮箱，可空。</summary>
    public string? ContactEmail { get; private set; }

    /// <summary>营业执照号，可空（个人卖家可无）。</summary>
    public string? BusinessLicenseNo { get; private set; }

    /// <summary>店铺经营地址，可空。</summary>
    public string? Address { get; private set; }

    /// <summary>店铺状态。</summary>
    public ShopStatus Status { get; private set; }

    /// <summary>在售商品数，由商品域事件驱动维护，不可为负。</summary>
    public int ProductCount { get; private set; }

    /// <summary>状态变更原因（暂停/关闭/驳回时记录）。</summary>
    public string? StatusReason { get; private set; }

    /// <summary>审核人标识（通过/驳回时记录）。</summary>
    public Guid? ReviewedBy { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private Shop() { }

    private Shop(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，卖家提交入驻申请时创建店铺，置状态为待审核，附加 <see cref="SellerRegisteredEvent"/>。
    /// </summary>
    /// <param name="shopId">店铺标识，由应用层生成。</param>
    /// <param name="userId">卖家账号标识（用户域 UserId）。</param>
    /// <param name="shopName">店铺名称。</param>
    /// <param name="contactPhone">客服电话。</param>
    /// <param name="contactEmail">客服邮箱，可空。</param>
    /// <param name="description">店铺描述，可空。</param>
    /// <param name="logo">店铺 Logo URL，可空。</param>
    /// <param name="businessLicenseNo">营业执照号，可空。</param>
    /// <param name="address">经营地址，可空。</param>
    public static Shop Create(
        Guid shopId,
        Guid userId,
        string shopName,
        string contactPhone,
        string? contactEmail = null,
        string? description = null,
        string? logo = null,
        string? businessLicenseNo = null,
        string? address = null)
    {
        if (shopId == Guid.Empty)
        {
            throw new SellerShopDomainException("店铺标识不可为空", "SHOP_ID_EMPTY");
        }

        if (userId == Guid.Empty)
        {
            throw new SellerShopDomainException("卖家标识不可为空", "SHOP_SELLER_EMPTY");
        }

        ValidateShopName(shopName);
        ValidatePhone(contactPhone);
        ValidateEmail(contactEmail);
        ValidateDescription(description);
        ValidateLogo(logo);
        ValidateBusinessLicenseNo(businessLicenseNo);
        ValidateAddress(address);

        var shop = new Shop(shopId)
        {
            SellerId = userId,
            ShopName = shopName.Trim(),
            ContactPhone = contactPhone.Trim(),
            ContactEmail = string.IsNullOrWhiteSpace(contactEmail) ? null : contactEmail.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            Logo = string.IsNullOrWhiteSpace(logo) ? null : logo.Trim(),
            BusinessLicenseNo = string.IsNullOrWhiteSpace(businessLicenseNo) ? null : businessLicenseNo.Trim(),
            Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim(),
            Status = ShopStatus.PendingReview,
            ProductCount = 0
        };

        // 卖家入驻申请提交：sellerId 与 userId 均引用用户域 UserId（卖家即用户）。
        shop.AddDomainEvent(new SellerRegisteredEvent(shop.Id, userId, userId, shop.ShopName));

        return shop;
    }

    /// <summary>
    /// 审核通过，仅待审核态可调用，流转至营业中，附加 <see cref="ShopApprovedEvent"/>。
    /// </summary>
    /// <param name="reviewedBy">审核人标识。</param>
    public void Approve(Guid reviewedBy)
    {
        if (Status != ShopStatus.PendingReview)
        {
            throw new SellerShopDomainException(
                $"当前状态为 {Status}，不可审核通过", "SHOP_INVALID_TRANSITION", 409);
        }

        if (reviewedBy == Guid.Empty)
        {
            throw new SellerShopDomainException("审核人标识不可为空", "SHOP_REVIEWER_EMPTY");
        }

        Status = ShopStatus.Active;
        ReviewedBy = reviewedBy;
        StatusReason = null;

        AddDomainEvent(new ShopApprovedEvent(Id, SellerId, ShopName));
    }

    /// <summary>
    /// 审核驳回，仅待审核态可调用，流转至已驳回。
    /// </summary>
    /// <param name="reviewedBy">审核人标识。</param>
    /// <param name="reason">驳回原因。</param>
    public void Reject(Guid reviewedBy, string reason)
    {
        if (Status != ShopStatus.PendingReview)
        {
            throw new SellerShopDomainException(
                $"当前状态为 {Status}，不可驳回", "SHOP_INVALID_TRANSITION", 409);
        }

        if (reviewedBy == Guid.Empty)
        {
            throw new SellerShopDomainException("审核人标识不可为空", "SHOP_REVIEWER_EMPTY");
        }

        ValidateReason(reason);

        Status = ShopStatus.Rejected;
        ReviewedBy = reviewedBy;
        StatusReason = reason.Trim();
    }

    /// <summary>
    /// 暂停店铺，仅营业中态可调用，流转至暂停，附加 <see cref="ShopSuspendedEvent"/>。
    /// 暂停后店铺商品不可售新单，既有订单正常履约。
    /// </summary>
    /// <param name="reason">暂停原因。</param>
    public void Suspend(string reason)
    {
        if (Status != ShopStatus.Active)
        {
            throw new SellerShopDomainException(
                $"当前状态为 {Status}，不可暂停", "SHOP_INVALID_TRANSITION", 409);
        }

        ValidateReason(reason);

        Status = ShopStatus.Suspended;
        StatusReason = reason.Trim();

        AddDomainEvent(new ShopSuspendedEvent(Id, SellerId));
    }

    /// <summary>
    /// 恢复店铺，仅暂停态可调用，流转至营业中，附加 <see cref="ShopResumedEvent"/>。
    /// </summary>
    public void Resume()
    {
        if (Status != ShopStatus.Suspended)
        {
            throw new SellerShopDomainException(
                $"当前状态为 {Status}，不可恢复", "SHOP_INVALID_TRANSITION", 409);
        }

        Status = ShopStatus.Active;
        StatusReason = null;

        AddDomainEvent(new ShopResumedEvent(Id, SellerId));
    }

    /// <summary>
    /// 关闭店铺，任意非关闭态可调用，流转至已关闭（终态），附加 <see cref="ShopClosedEvent"/>。
    /// 关闭后店铺不可恢复经营，商品全部下架。
    /// </summary>
    /// <param name="reason">关闭原因。</param>
    public void Close(string reason)
    {
        if (Status == ShopStatus.Closed)
        {
            throw new SellerShopDomainException("店铺已关闭，不可重复关闭", "SHOP_ALREADY_CLOSED", 409);
        }

        ValidateReason(reason);

        Status = ShopStatus.Closed;
        StatusReason = reason.Trim();

        AddDomainEvent(new ShopClosedEvent(Id, SellerId));
    }

    /// <summary>
    /// 更新店铺基础信息（名称、描述、地址），任意非关闭态可调用。
    /// </summary>
    public void UpdateInfo(string shopName, string? description, string? address)
    {
        EnsureNotClosed();

        ValidateShopName(shopName);
        ValidateDescription(description);
        ValidateAddress(address);

        ShopName = shopName.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
    }

    /// <summary>
    /// 更新店铺 Logo，任意非关闭态可调用。
    /// </summary>
    public void UpdateLogo(string? logo)
    {
        EnsureNotClosed();

        ValidateLogo(logo);

        Logo = string.IsNullOrWhiteSpace(logo) ? null : logo.Trim();
    }

    /// <summary>
    /// 更新客服联系方式，任意非关闭态可调用。
    /// </summary>
    public void UpdateContact(string contactPhone, string? contactEmail)
    {
        EnsureNotClosed();

        ValidatePhone(contactPhone);
        ValidateEmail(contactEmail);

        ContactPhone = contactPhone.Trim();
        ContactEmail = string.IsNullOrWhiteSpace(contactEmail) ? null : contactEmail.Trim();
    }

    /// <summary>
    /// 商品上架时商品数 +1，由商品域 ProductPublishedEvent 驱动。
    /// </summary>
    public void IncrementProductCount()
    {
        ProductCount++;
    }

    /// <summary>
    /// 商品下架时商品数 -1，由商品域 ProductTakenDownEvent 驱动，不可为负。
    /// </summary>
    public void DecrementProductCount()
    {
        if (ProductCount <= 0)
        {
            return;
        }

        ProductCount--;
    }

    private void EnsureNotClosed()
    {
        if (Status == ShopStatus.Closed)
        {
            throw new SellerShopDomainException("已关闭的店铺不可修改信息", "SHOP_CLOSED", 409);
        }
    }

    private static void ValidateShopName(string shopName)
    {
        if (string.IsNullOrWhiteSpace(shopName))
        {
            throw new SellerShopDomainException("店铺名称不可为空", "SHOP_NAME_EMPTY");
        }

        var trimmed = shopName.Trim();
        if (trimmed.Length is < MinShopNameLength or > MaxShopNameLength)
        {
            throw new SellerShopDomainException(
                $"店铺名称长度须为 {MinShopNameLength}-{MaxShopNameLength} 字符", "SHOP_NAME_LENGTH");
        }
    }

    private static void ValidatePhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            throw new SellerShopDomainException("客服电话不可为空", "SHOP_PHONE_EMPTY");
        }

        if (phone.Trim().Length > MaxPhoneLength)
        {
            throw new SellerShopDomainException($"客服电话长度不可超过 {MaxPhoneLength} 字符", "SHOP_PHONE_LENGTH");
        }
    }

    private static void ValidateEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        var trimmed = email.Trim();
        if (trimmed.Length > MaxEmailLength)
        {
            throw new SellerShopDomainException($"客服邮箱长度不可超过 {MaxEmailLength} 字符", "SHOP_EMAIL_LENGTH");
        }

        if (!trimmed.Contains('@') || trimmed.StartsWith('@') || trimmed.EndsWith('@'))
        {
            throw new SellerShopDomainException("客服邮箱格式不正确", "SHOP_EMAIL_FORMAT");
        }
    }

    private static void ValidateDescription(string? description)
    {
        if (!string.IsNullOrWhiteSpace(description) && description.Trim().Length > MaxDescriptionLength)
        {
            throw new SellerShopDomainException(
                $"店铺描述长度不可超过 {MaxDescriptionLength} 字符", "SHOP_DESCRIPTION_LENGTH");
        }
    }

    private static void ValidateLogo(string? logo)
    {
        if (string.IsNullOrWhiteSpace(logo))
        {
            return;
        }

        if (logo.Trim().Length > MaxLogoLength)
        {
            throw new SellerShopDomainException($"Logo URL 长度不可超过 {MaxLogoLength} 字符", "SHOP_LOGO_LENGTH");
        }
    }

    private static void ValidateBusinessLicenseNo(string? licenseNo)
    {
        if (!string.IsNullOrWhiteSpace(licenseNo) && licenseNo.Trim().Length > MaxBusinessLicenseNoLength)
        {
            throw new SellerShopDomainException(
                $"营业执照号长度不可超过 {MaxBusinessLicenseNoLength} 字符", "SHOP_LICENSE_LENGTH");
        }
    }

    private static void ValidateAddress(string? address)
    {
        if (!string.IsNullOrWhiteSpace(address) && address.Trim().Length > MaxAddressLength)
        {
            throw new SellerShopDomainException(
                $"经营地址长度不可超过 {MaxAddressLength} 字符", "SHOP_ADDRESS_LENGTH");
        }
    }

    private static void ValidateReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new SellerShopDomainException("操作原因不可为空", "SHOP_REASON_EMPTY");
        }

        if (reason.Trim().Length > MaxReasonLength)
        {
            throw new SellerShopDomainException(
                $"操作原因长度不可超过 {MaxReasonLength} 字符", "SHOP_REASON_LENGTH");
        }
    }
}
