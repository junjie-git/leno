namespace Leno.SellerShop.Application.DTOs;

/// <summary>
/// 带原因的操作 DTO，用于店铺/卖家档案的驳回、暂停、关闭等操作。
/// </summary>
public sealed class ActionReasonDto
{
    /// <summary>操作原因，不可为空，≤200 字符。</summary>
    public string Reason { get; init; } = string.Empty;
}
