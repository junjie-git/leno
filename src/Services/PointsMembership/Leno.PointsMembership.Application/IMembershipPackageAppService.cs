using Leno.PointsMembership.Application.DTOs;

namespace Leno.PointsMembership.Application;

/// <summary>
/// 会员套餐管理应用服务，编排套餐查询与运营端 CRUD、启停及用户订阅用例。
/// </summary>
public interface IMembershipPackageAppService
{
    /// <summary>
    /// 查询全部已启用会员套餐，供买家购买页展示。
    /// </summary>
    Task<List<MembershipPackageDto>> GetPackagesAsync(CancellationToken ct = default);

    /// <summary>
    /// 创建会员套餐。
    /// </summary>
    Task<MembershipPackageDto> CreatePackageAsync(CreateMembershipPackageDto dto, CancellationToken ct = default);

    /// <summary>
    /// 更新会员套餐（名称、价格、时长、权益）。
    /// </summary>
    /// <param name="packageId">套餐标识。</param>
    Task<MembershipPackageDto> UpdatePackageAsync(Guid packageId, UpdateMembershipPackageDto dto, CancellationToken ct = default);

    /// <summary>
    /// 启用会员套餐。
    /// </summary>
    Task EnablePackageAsync(Guid packageId, CancellationToken ct = default);

    /// <summary>
    /// 停用会员套餐。
    /// </summary>
    Task DisablePackageAsync(Guid packageId, CancellationToken ct = default);

    /// <summary>
    /// 用户订阅套餐，创建待支付的用户会员权益记录，实际订单创建转发至订单域。
    /// </summary>
    /// <param name="userId">订阅用户标识。</param>
    /// <param name="packageId">套餐标识。</param>
    Task SubscribeAsync(Guid userId, Guid packageId, CancellationToken ct = default);
}
