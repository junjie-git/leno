using Leno.Membership.Application.DTOs;

namespace Leno.Membership.Application;

/// <summary>
/// 会员管理应用服务接口，封装会员信息查询与运营端等级定义 CRUD 用例。
/// </summary>
public interface IMemberAppService
{
    /// <summary>
    /// 获取用户会员档案，若不存在则抛域异常。
    /// </summary>
    /// <param name="userId">用户标识。</param>
    /// <param name="ct">取消令牌。</param>
    Task<MemberDto> GetMemberInfoAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// 获取全部会员等级定义，按成长值门槛升序。
    /// </summary>
    Task<List<MemberLevelDefinitionDto>> GetLevelsAsync(CancellationToken ct = default);

    /// <summary>
    /// 创建会员等级定义。
    /// </summary>
    Task<MemberLevelDefinitionDto> CreateLevelAsync(CreateMemberLevelDefinitionDto dto, CancellationToken ct = default);

    /// <summary>
    /// 更新会员等级定义（等级编号不可改）。
    /// </summary>
    Task<MemberLevelDefinitionDto> UpdateLevelAsync(Guid levelId, UpdateMemberLevelDefinitionDto dto, CancellationToken ct = default);
}

/// <summary>
/// 会员套餐管理应用服务接口，封装套餐查询与运营端 CRUD、启停用例。
/// </summary>
public interface IMembershipPackageAppService
{
    /// <summary>
    /// 获取全部已启用的会员套餐，供买家购买页展示。
    /// </summary>
    Task<List<MembershipPackageDto>> GetPackagesAsync(CancellationToken ct = default);

    /// <summary>
    /// 创建会员套餐。
    /// </summary>
    Task<MembershipPackageDto> CreatePackageAsync(CreateMembershipPackageDto dto, CancellationToken ct = default);

    /// <summary>
    /// 更新会员套餐（等级编号不可改）。
    /// </summary>
    Task<MembershipPackageDto> UpdatePackageAsync(Guid packageId, UpdateMembershipPackageDto dto, CancellationToken ct = default);

    /// <summary>
    /// 启用套餐。
    /// </summary>
    Task EnablePackageAsync(Guid packageId, CancellationToken ct = default);

    /// <summary>
    /// 停用套餐，停用后不可购买。
    /// </summary>
    Task DisablePackageAsync(Guid packageId, CancellationToken ct = default);
}
