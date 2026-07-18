using System.Text.RegularExpressions;
using Leno.SharedKernel.Abstractions;
using Leno.UserAuth.Domain.Exceptions;
using Leno.UserAuth.Domain.ValueObjects;

namespace Leno.UserAuth.Domain.Aggregates;

/// <summary>
/// 收货地址聚合根，封装收货地址详情与默认地址标记。
/// 同一用户下默认地址唯一的不变量由应用层调用 <see cref="MarkAsDefault"/>/<see cref="UnmarkDefault"/> 协调保证。
/// </summary>
public sealed partial class Address : AggregateRoot
{
    private const int MaxTagLength = 8;

    /// <summary>所属用户标识。</summary>
    public Guid UserId { get; private set; }

    /// <summary>收件人姓名，1–32 字符。</summary>
    public string RecipientName { get; private set; } = string.Empty;

    /// <summary>收件人手机号（E.164）。</summary>
    public string RecipientPhone { get; private set; } = string.Empty;

    /// <summary>省/直辖市。</summary>
    public string Province { get; private set; } = string.Empty;

    /// <summary>市。</summary>
    public string City { get; private set; } = string.Empty;

    /// <summary>区/县。</summary>
    public string District { get; private set; } = string.Empty;

    /// <summary>详细地址，5–200 字符（经 <see cref="AddressDetail"/> 校验）。</summary>
    public string Detail { get; private set; } = string.Empty;

    /// <summary>地址标签（如"家""公司"），可空，≤8 字符。</summary>
    public string? Tag { get; private set; }

    /// <summary>是否默认地址。</summary>
    public bool IsDefault { get; private set; }

    /// <summary>地址状态。</summary>
    public AddressStatus Status { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private Address() { }

    private Address(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，创建处于 Active 状态的地址。
    /// </summary>
    public static Address Create(
        Guid id,
        Guid userId,
        string recipientName,
        string recipientPhone,
        string province,
        string city,
        string district,
        string detail,
        string? tag = null,
        bool isDefault = false)
    {
        if (id == Guid.Empty)
        {
            throw new UserAuthDomainException("地址标识不可为空", "ADDRESS_ID_EMPTY");
        }

        if (userId == Guid.Empty)
        {
            throw new UserAuthDomainException("用户标识不可为空", "ADDRESS_USER_EMPTY");
        }

        ValidateRecipientName(recipientName);
        ValidatePhone(recipientPhone);
        ValidateRegion(province, city, district);
        var validatedDetail = AddressDetail.Create(detail);
        ValidateTag(tag);

        return new Address(id)
        {
            UserId = userId,
            RecipientName = recipientName.Trim(),
            RecipientPhone = recipientPhone.Trim(),
            Province = province.Trim(),
            City = city.Trim(),
            District = district.Trim(),
            Detail = validatedDetail.Value,
            Tag = string.IsNullOrWhiteSpace(tag) ? null : tag.Trim(),
            IsDefault = isDefault,
            Status = AddressStatus.Active
        };
    }

    /// <summary>更新地址可变字段。</summary>
    public void UpdateInfo(
        string recipientName,
        string recipientPhone,
        string province,
        string city,
        string district,
        string detail,
        string? tag = null)
    {
        EnsureActive();

        ValidateRecipientName(recipientName);
        ValidatePhone(recipientPhone);
        ValidateRegion(province, city, district);
        var validatedDetail = AddressDetail.Create(detail);
        ValidateTag(tag);

        RecipientName = recipientName.Trim();
        RecipientPhone = recipientPhone.Trim();
        Province = province.Trim();
        City = city.Trim();
        District = district.Trim();
        Detail = validatedDetail.Value;
        Tag = string.IsNullOrWhiteSpace(tag) ? null : tag.Trim();
    }

    /// <summary>置为默认地址。</summary>
    public void MarkAsDefault()
    {
        EnsureActive();
        IsDefault = true;
    }

    /// <summary>取消默认地址标记。</summary>
    public void UnmarkDefault()
    {
        IsDefault = false;
    }

    /// <summary>软删除地址，置 Status 为 Deleted。</summary>
    public void SoftDelete()
    {
        if (Status == AddressStatus.Deleted)
        {
            throw new UserAuthDomainException("地址已删除", "ADDRESS_ALREADY_DELETED");
        }

        Status = AddressStatus.Deleted;
        IsDefault = false;
    }

    private void EnsureActive()
    {
        if (Status != AddressStatus.Active)
        {
            throw new UserAuthDomainException("仅 Active 状态的地址可修改", "ADDRESS_NOT_ACTIVE");
        }
    }

    private static void ValidateRecipientName(string recipientName)
    {
        if (string.IsNullOrWhiteSpace(recipientName))
        {
            throw new UserAuthDomainException("收件人不可为空", "ADDRESS_RECIPIENT_EMPTY");
        }

        if (recipientName.Trim().Length is < 1 or > 32)
        {
            throw new UserAuthDomainException("收件人姓名长度须为 1-32 字符", "ADDRESS_RECIPIENT_LENGTH");
        }
    }

    private static void ValidatePhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            throw new UserAuthDomainException("收件人手机号不可为空", "ADDRESS_PHONE_EMPTY");
        }

        if (!ValidPhonePattern().IsMatch(phone.Trim()))
        {
            throw new UserAuthDomainException("收件人手机号须为 E.164 格式", "ADDRESS_PHONE_FORMAT");
        }
    }

    private static void ValidateRegion(string province, string city, string district)
    {
        if (string.IsNullOrWhiteSpace(province))
        {
            throw new UserAuthDomainException("省/直辖市不可为空", "ADDRESS_PROVINCE_EMPTY");
        }

        if (string.IsNullOrWhiteSpace(city))
        {
            throw new UserAuthDomainException("市不可为空", "ADDRESS_CITY_EMPTY");
        }

        if (string.IsNullOrWhiteSpace(district))
        {
            throw new UserAuthDomainException("区/县不可为空", "ADDRESS_DISTRICT_EMPTY");
        }
    }

    private static void ValidateTag(string? tag)
    {
        if (!string.IsNullOrWhiteSpace(tag) && tag.Trim().Length > MaxTagLength)
        {
            throw new UserAuthDomainException($"地址标签长度不可超过 {MaxTagLength} 字符", "ADDRESS_TAG_LENGTH");
        }
    }

    [GeneratedRegex(@"^\+[1-9]\d{1,14}$", RegexOptions.CultureInvariant)]
    private static partial Regex ValidPhonePattern();
}
