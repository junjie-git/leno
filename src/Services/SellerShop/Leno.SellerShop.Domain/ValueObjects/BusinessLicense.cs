namespace Leno.SellerShop.Domain.ValueObjects;

/// <summary>
/// 营业执照值对象，承载证照编号、图片 URL 与失效日期，不可变。
/// 相等性由 LicenseNo 与 ImageUrl 联合判定。
/// </summary>
public sealed record BusinessLicense
{
    private const int MaxLicenseNoLength = 32;
    private const int MaxImageUrlLength = 512;

    /// <summary>证照编号。</summary>
    public string LicenseNo { get; init; }

    /// <summary>证照图片 URL。</summary>
    public string ImageUrl { get; init; }

    /// <summary>失效日期（UTC），可空表示长期有效。</summary>
    public DateTime? ExpireDate { get; init; }

    private BusinessLicense()
    {
        LicenseNo = string.Empty;
        ImageUrl = string.Empty;
    }

    public BusinessLicense(string licenseNo, string imageUrl, DateTime? expireDate)
    {
        if (string.IsNullOrWhiteSpace(licenseNo))
        {
            throw new ArgumentException("证照编号不可为空", nameof(licenseNo));
        }

        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            throw new ArgumentException("证照图片 URL 不可为空", nameof(imageUrl));
        }

        if (licenseNo.Trim().Length > MaxLicenseNoLength)
        {
            throw new ArgumentException($"证照编号长度不可超过 {MaxLicenseNoLength} 字符", nameof(licenseNo));
        }

        if (imageUrl.Trim().Length > MaxImageUrlLength)
        {
            throw new ArgumentException($"证照图片 URL 长度不可超过 {MaxImageUrlLength} 字符", nameof(imageUrl));
        }

        LicenseNo = licenseNo.Trim();
        ImageUrl = imageUrl.Trim();
        ExpireDate = expireDate;
    }

    /// <summary>判断证照是否在指定时刻仍有效（未过期）。</summary>
    public bool IsValidAt(DateTime utcNow) => !ExpireDate.HasValue || ExpireDate.Value > utcNow;
}
