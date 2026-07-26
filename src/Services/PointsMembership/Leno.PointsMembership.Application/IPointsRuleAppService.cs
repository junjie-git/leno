using Leno.PointsMembership.Application.DTOs;

namespace Leno.PointsMembership.Application;

/// <summary>
/// 积分规则管理应用服务，编排运营端规则 CRUD、启停用例。
/// 对应 design-prompts operations/08-membership-ops/points-rules.md 的 5 个规划端点。
/// </summary>
public interface IPointsRuleAppService
{
    /// <summary>
    /// 查询全部积分规则（含停用），按创建时间升序。
    /// 对应 GET /api/admin/points/rules。
    /// </summary>
    Task<List<PointsRuleDto>> GetRulesAsync(CancellationToken ct = default);

    /// <summary>
    /// 创建积分规则，编码唯一约束冲突时抛出 <c>PointsDomainException</c>（错误码 POINTS_RULE_CODE_EXISTS，映射 409）。
    /// 对应 POST /api/admin/points/rules。
    /// </summary>
    Task<PointsRuleDto> CreateRuleAsync(CreatePointsRuleDto dto, CancellationToken ct = default);

    /// <summary>
    /// 更新积分规则（名称、行为类型、积分值、每日上限），编码不可改，状态经启用/停用端点切换。
    /// 支持正负积分值（发放/扣减）。
    /// 对应 PUT /api/admin/points/rules/{ruleId}。
    /// </summary>
    /// <param name="ruleId">规则标识。</param>
    Task<PointsRuleDto> UpdateRuleAsync(Guid ruleId, UpdatePointsRuleDto dto, CancellationToken ct = default);

    /// <summary>
    /// 启用积分规则，已启用时抛出 <c>PointsDomainException</c>（错误码 POINTS_RULE_ALREADY_ENABLED，映射 409）。
    /// 对应 POST /api/admin/points/rules/{ruleId}/enable。
    /// </summary>
    /// <param name="ruleId">规则标识。</param>
    Task EnableRuleAsync(Guid ruleId, CancellationToken ct = default);

    /// <summary>
    /// 停用积分规则，已停用时抛出 <c>PointsDomainException</c>（错误码 POINTS_RULE_ALREADY_DISABLED，映射 409）。
    /// 对应 POST /api/admin/points/rules/{ruleId}/disable。
    /// </summary>
    /// <param name="ruleId">规则标识。</param>
    Task DisableRuleAsync(Guid ruleId, CancellationToken ct = default);
}
