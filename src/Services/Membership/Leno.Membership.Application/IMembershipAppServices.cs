using Leno.Membership.Application.DTOs;

namespace Leno.Membership.Application;

/// <summary>
/// 会员管理应用服务接口，封装会员信息查询与运营端等级定义 CRUD、启停用例。
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

    /// <summary>
    /// 启用会员等级定义，已启用返回域异常。
    /// </summary>
    /// <param name="levelId">等级定义标识。</param>
    /// <param name="ct">取消令牌。</param>
    Task EnableLevelAsync(Guid levelId, CancellationToken ct = default);

    /// <summary>
    /// 停用会员等级定义，停用后不参与等级评估，已停用返回域异常。
    /// </summary>
    /// <param name="levelId">等级定义标识。</param>
    /// <param name="ct">取消令牌。</param>
    Task DisableLevelAsync(Guid levelId, CancellationToken ct = default);
}

/// <summary>
/// 会员套餐管理应用服务接口，封装套餐查询与运营端 CRUD、启停、买家订阅用例。
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

    /// <summary>
    /// 买家订阅会员套餐，校验套餐存在且已启用，生成待支付订阅意图，实际订单创建转发至订单域。
    /// </summary>
    /// <param name="userId">订阅用户标识，由 Controller 从 JWT 注入。</param>
    /// <param name="packageId">套餐标识，由路由参数传入。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>订阅意图结果，承载套餐快照与订阅标识，订单域据此创建订单。</returns>
    Task<SubscriptionResultDto> SubscribeAsync(Guid userId, Guid packageId, CancellationToken ct = default);
}
