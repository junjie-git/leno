using Leno.PointsMembership.Application.DTOs;

namespace Leno.PointsMembership.Application;

/// <summary>
/// 会员管理应用服务，编排会员信息查询与运营端等级 CRUD、启停用例。
/// </summary>
public interface IMemberAppService
{
    /// <summary>
    /// 查询当前用户会员信息。
    /// </summary>
    /// <param name="userId">用户标识。</param>
    Task<MemberDto> GetMemberInfoAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// 查询全部会员等级（按等级编号升序）。
    /// </summary>
    Task<List<MembershipLevelDto>> GetLevelsAsync(CancellationToken ct = default);

    /// <summary>
    /// 创建会员等级。
    /// </summary>
    Task<MembershipLevelDto> CreateLevelAsync(CreateMembershipLevelDto dto, CancellationToken ct = default);

    /// <summary>
    /// 更新会员等级（名称、门槛、折扣率）。
    /// </summary>
    /// <param name="levelId">等级标识。</param>
    Task<MembershipLevelDto> UpdateLevelAsync(Guid levelId, UpdateMembershipLevelDto dto, CancellationToken ct = default);

    /// <summary>
    /// 启用会员等级。
    /// </summary>
    Task EnableLevelAsync(Guid levelId, CancellationToken ct = default);

    /// <summary>
    /// 停用会员等级。
    /// </summary>
    Task DisableLevelAsync(Guid levelId, CancellationToken ct = default);
}
