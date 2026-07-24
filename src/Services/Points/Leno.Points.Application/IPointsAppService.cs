using Leno.Points.Application.DTOs;
using Leno.Points.Domain.Aggregates.PointsExchange;

namespace Leno.Points.Application;

/// <summary>
/// 积分应用服务接口，封装积分账户与兑换的用例编排。
/// </summary>
public interface IPointsAppService
{
    /// <summary>
    /// 获取用户积分账户，若不存在则创建。
    /// </summary>
    /// <param name="userId">用户标识。</param>
    /// <param name="ct">取消令牌。</param>
    Task<PointsAccountDto> GetOrCreateAccountAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// 增加用户积分（来源签到/活动/任务等）。
    /// </summary>
    Task<PointsAccountDto> EarnAsync(Guid userId, Domain.ValueObjects.PointsSource source, int amount, string reason, CancellationToken ct = default);

    /// <summary>
    /// 分页查询用户积分流水。
    /// </summary>
    Task<List<PointsFlowDto>> GetFlowsAsync(Guid userId, int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// 请求积分兑换，扣减积分并创建兑换聚合。
    /// </summary>
    Task<Guid> RequestExchangeAsync(ExchangePointsRequestDto request, CancellationToken ct = default);

    /// <summary>
    /// 确认兑换完成。
    /// </summary>
    Task CompleteExchangeAsync(Guid exchangeId, CancellationToken ct = default);

    /// <summary>
    /// 标记兑换失败并回补积分。
    /// </summary>
    Task FailExchangeAsync(Guid exchangeId, string reason, CancellationToken ct = default);
}

/// <summary>
/// 积分内部应用服务接口，供其他 BC 经 gRPC 调用的内部用例。
/// </summary>
public interface IPointsInternalAppService
{
    /// <summary>
    /// 等级提升奖励积分入账（消费 MemberLevelChangedIntegrationEvent 后调用）。
    /// </summary>
    /// <param name="userId">用户标识。</param>
    /// <param name="amount">奖励积分数量。</param>
    /// <param name="newLevel">触发奖励的会员等级编号。</param>
    /// <param name="ct">取消令牌。</param>
    Task GrantLevelBonusAsync(Guid userId, int amount, int newLevel, CancellationToken ct = default);
}
