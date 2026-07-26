using Leno.UserCenter.Application.DTOs;

namespace Leno.UserCenter.Application;

/// <summary>
/// 收货地址应用服务，编排地址增删改查与默认地址切换用例。
/// 从 UserAuth BC 迁入 UserCenter BC（Task A6）。
/// </summary>
public interface IAddressAppService
{
    /// <summary>查询用户地址列表（默认地址优先）。</summary>
    Task<IReadOnlyList<AddressDto>> ListAsync(Guid userId, CancellationToken ct = default);

    /// <summary>新增收货地址。</summary>
    Task<AddressDto> CreateAsync(Guid userId, SaveAddressDto dto, CancellationToken ct = default);

    /// <summary>修改收货地址。</summary>
    Task<AddressDto> UpdateAsync(Guid userId, Guid addressId, SaveAddressDto dto, CancellationToken ct = default);

    /// <summary>软删除收货地址。</summary>
    Task DeleteAsync(Guid userId, Guid addressId, CancellationToken ct = default);

    /// <summary>将指定地址设为默认。</summary>
    Task<AddressDto> SetDefaultAsync(Guid userId, Guid addressId, CancellationToken ct = default);
}
