namespace Leno.SellerShop.Application.DTOs;

/// <summary>
/// 卖家入驻申请 DTO，同时创建店铺与卖家档案并置待审核。
/// </summary>
public sealed class SubmitShopApplicationDto
{
    /// <summary>店铺名称，2-32 字符。</summary>
    public string ShopName { get; init; } = string.Empty;

    /// <summary>客服电话。</summary>
    public string ContactPhone { get; init; } = string.Empty;

    /// <summary>客服邮箱，可空。</summary>
    public string? ContactEmail { get; init; }

    /// <summary>店铺描述，可空。</summary>
    public string? Description { get; init; }

    /// <summary>店铺 Logo URL，可空。</summary>
    public string? Logo { get; init; }

    /// <summary>经营地址，可空。</summary>
    public string? Address { get; init; }

    /// <summary>营业执照号，可空（个人卖家可无，但须与身份证号二选一）。</summary>
    public string? BusinessLicenseNo { get; init; }

    /// <summary>真实姓名。</summary>
    public string RealName { get; init; } = string.Empty;

    /// <summary>身份证号，可空（企业卖家可无，但须与营业执照号二选一）。</summary>
    public string? IdCard { get; init; }

    /// <summary>收款银行账号，可空。</summary>
    public string? BankAccount { get; init; }
}
