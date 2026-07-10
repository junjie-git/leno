using Leno.Order.Domain.Exceptions;

namespace Leno.Order.Domain.ValueObjects;

/// <summary>
/// 收货地址快照值对象，下单时固化地址信息，避免地址变更影响历史订单。
/// 采用 sealed class（非 record）以便 EF Core 作为 owned type 映射。
/// </summary>
public sealed class AddressSnapshot
{
    /// <summary>收件人姓名。</summary>
    public string RecipientName { get; private set; } = string.Empty;

    /// <summary>收件人手机号。</summary>
    public string RecipientPhone { get; private set; } = string.Empty;

    /// <summary>省份。</summary>
    public string Province { get; private set; } = string.Empty;

    /// <summary>城市。</summary>
    public string City { get; private set; } = string.Empty;

    /// <summary>区/县。</summary>
    public string District { get; private set; } = string.Empty;

    /// <summary>详细地址。</summary>
    public string Detail { get; private set; } = string.Empty;

    /// <summary>EF Core 无参构造。</summary>
    private AddressSnapshot() { }

    private AddressSnapshot(string recipientName, string recipientPhone, string province, string city, string district, string detail)
    {
        RecipientName = recipientName;
        RecipientPhone = recipientPhone;
        Province = province;
        City = city;
        District = district;
        Detail = detail;
    }

    /// <summary>
    /// 工厂方法，校验各字段非空后创建地址快照。
    /// </summary>
    /// <param name="recipientName">收件人姓名。</param>
    /// <param name="recipientPhone">收件人手机号。</param>
    /// <param name="province">省份。</param>
    /// <param name="city">城市。</param>
    /// <param name="district">区/县。</param>
    /// <param name="detail">详细地址。</param>
    public static AddressSnapshot Create(
        string recipientName,
        string recipientPhone,
        string province,
        string city,
        string district,
        string detail)
    {
        if (string.IsNullOrWhiteSpace(recipientName))
        {
            throw new OrderDomainException("收件人姓名不可为空", "ORDER_ADDRESS_NAME_EMPTY");
        }

        if (string.IsNullOrWhiteSpace(recipientPhone))
        {
            throw new OrderDomainException("收件人手机号不可为空", "ORDER_ADDRESS_PHONE_EMPTY");
        }

        if (string.IsNullOrWhiteSpace(province))
        {
            throw new OrderDomainException("省份不可为空", "ORDER_ADDRESS_PROVINCE_EMPTY");
        }

        if (string.IsNullOrWhiteSpace(city))
        {
            throw new OrderDomainException("城市不可为空", "ORDER_ADDRESS_CITY_EMPTY");
        }

        if (string.IsNullOrWhiteSpace(district))
        {
            throw new OrderDomainException("区/县不可为空", "ORDER_ADDRESS_DISTRICT_EMPTY");
        }

        if (string.IsNullOrWhiteSpace(detail))
        {
            throw new OrderDomainException("详细地址不可为空", "ORDER_ADDRESS_DETAIL_EMPTY");
        }

        return new AddressSnapshot(recipientName, recipientPhone, province, city, district, detail);
    }
}
