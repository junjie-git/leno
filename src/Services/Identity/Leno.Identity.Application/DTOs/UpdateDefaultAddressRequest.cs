namespace Leno.Identity.Application.DTOs;

/// <summary>
/// 更新默认收货地址请求（internal API，UserCenter 经防腐层调用）。
/// </summary>
public sealed class UpdateDefaultAddressRequest
{
    /// <summary>默认地址标识；null 表示清除默认地址。</summary>
    public Guid? AddressId { get; set; }
}
