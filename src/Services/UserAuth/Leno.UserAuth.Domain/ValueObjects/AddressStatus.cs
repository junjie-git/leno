namespace Leno.UserAuth.Domain.ValueObjects;

/// <summary>
/// 收货地址状态枚举。
/// 地址删除为软删（Active → Deleted），保留以供订单快照追溯，不物理删除。
/// </summary>
public enum AddressStatus
{
    /// <summary>正常：可被选用为收货地址。</summary>
    Active = 1,

    /// <summary>已删除：软删态，列表查询不返回。</summary>
    Deleted = 2
}
