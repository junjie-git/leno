namespace Leno.SellerShop.Application.DTOs;

/// <summary>
/// 卖家档案提交/更新 DTO。
/// </summary>
public sealed class SubmitSellerProfileDto
{
    /// <summary>真实姓名。</summary>
    public string RealName { get; init; } = string.Empty;

    /// <summary>身份证号，可空。</summary>
    public string? IdCard { get; init; }

    /// <summary>营业执照号，可空。</summary>
    public string? BusinessLicenseNo { get; init; }

    /// <summary>收款银行账号，可空。</summary>
    public string? BankAccount { get; init; }
}
