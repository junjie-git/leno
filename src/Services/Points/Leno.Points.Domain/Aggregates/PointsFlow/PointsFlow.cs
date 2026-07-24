using Leno.Points.Domain.Exceptions;
using Leno.Points.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.Points.Domain.Aggregates.PointsFlow;

/// <summary>
/// 积分流水值对象（聚合子实体），记录积分账户单笔变动的明细，由聚合根在状态变更时同事务追加。
/// 聚合标识 <see cref="Entity.Id"/> 即对外 <c>FlowId</c>。
/// 流水类型 <see cref="PointsTxType"/> 涵盖获得/消耗/过期/冻结/释放等全部场景。
/// </summary>
public sealed class PointsFlow : Entity
{
    /// <summary>所属积分账户标识。</summary>
    public Guid AccountId { get; private set; }

    /// <summary>交易类型。</summary>
    public PointsTxType TxType { get; private set; }

    /// <summary>变动积分数量（正数）。</summary>
    public int Amount { get; private set; }

    /// <summary>交易后账户可用余额。</summary>
    public int BalanceAfter { get; private set; }

    /// <summary>积分来源。</summary>
    public PointsSource Source { get; private set; }

    /// <summary>关联业务标识（签到记录/订单/活动等）。</summary>
    public Guid ReferenceId { get; private set; }

    /// <summary>变动原因描述。</summary>
    public string Reason { get; private set; } = string.Empty;

    /// <summary>发生时间（UTC）。</summary>
    public DateTime OccurredAt { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private PointsFlow() { }

    private PointsFlow(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，校验账户标识非空。
    /// </summary>
    /// <param name="flowId">流水标识，由应用层生成。</param>
    /// <param name="accountId">所属账户标识。</param>
    /// <param name="txType">交易类型。</param>
    /// <param name="amount">变动数量。</param>
    /// <param name="balanceAfter">交易后余额。</param>
    /// <param name="source">积分来源。</param>
    /// <param name="referenceId">关联业务标识。</param>
    /// <param name="reason">变动原因。</param>
    /// <param name="occurredAt">发生时间（UTC）。</param>
    public static PointsFlow Create(
        Guid flowId,
        Guid accountId,
        PointsTxType txType,
        int amount,
        int balanceAfter,
        PointsSource source,
        Guid referenceId,
        string reason,
        DateTime occurredAt)
    {
        if (accountId == Guid.Empty)
        {
            throw new PointsDomainException("AccountId 不可为空", "POINTS_ACCOUNT_EMPTY");
        }

        return new PointsFlow(flowId == Guid.Empty ? Guid.NewGuid() : flowId)
        {
            AccountId = accountId,
            TxType = txType,
            Amount = amount,
            BalanceAfter = balanceAfter,
            Source = source,
            ReferenceId = referenceId,
            Reason = reason,
            OccurredAt = occurredAt
        };
    }
}
