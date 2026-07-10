namespace Leno.SellerShop.Application.DTOs;

/// <summary>
/// 店铺信息更新 DTO，卖家修改店铺基础资料、Logo 与联系方式。
/// </summary>
public sealed class UpdateShopInfoDto
{
    /// <summary>店铺名称，2-32 字符。</summary>
    public string ShopName { get; init; } = string.Empty;

    /// <summary>店铺描述，可空。</summary>
    public string? Description { get; init; }

    /// <summary>经营地址，可空。</summary>
    public string? Address { get; init; }

    /// <summary>店铺 Logo URL，可空。</summary>
    public string? Logo { get; init; }

    /// <summary>客服电话。</summary>
    public string ContactPhone { get; init; } = string.Empty;

    /// <summary>客服邮箱，可空。</summary>
    public string? ContactEmail { get; init; }
}
