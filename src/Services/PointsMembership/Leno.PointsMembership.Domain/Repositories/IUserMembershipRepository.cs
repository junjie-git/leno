using Leno.PointsMembership.Domain.Aggregates;
using Leno.PointsMembership.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using UserMembershipAggregate = Leno.PointsMembership.Domain.Aggregates.UserMembership;

namespace Leno.PointsMembership.Domain.Repositories;

/// <summary>
/// 用户会员权益仓储接口，管理 <see cref="UserMembership"/> 聚合。
/// </summary>
public interface IUserMembershipRepository : IRepository<UserMembershipAggregate>
{
    /// <summary>
    /// 按订单标识查询用户会员权益，用于支付回调激活权益。
    /// </summary>
    /// <param name="orderId">订单标识。</param>
    Task<UserMembershipAggregate?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default);

    /// <summary>
    /// 按用户标识查询当前生效中的会员权益。
    /// </summary>
    /// <param name="userId">用户标识。</param>
    Task<UserMembershipAggregate?> GetActiveByUserIdAsync(Guid userId, CancellationToken ct = default);
}
