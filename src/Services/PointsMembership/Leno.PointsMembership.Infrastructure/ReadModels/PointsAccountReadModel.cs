namespace Leno.PointsMembership.Infrastructure.ReadModels;

/// <summary>
/// 积分账户 ES 读模型文档，索引名 <see cref="PointsAccountIndexName"/>。
/// 用于用户端积分账户查询与运营分析的快速检索。
/// 写侧账户创建时由 <see cref="PointsAccountCreatedReadModelSyncConsumer"/> 索引；
/// 账户余额变化时由 <see cref="PointsAdjustedReadModelSyncConsumer"/> 重建（IndexAsync 覆盖更新）。
/// </summary>
public sealed class PointsAccountReadModel
{
    /// <summary>积分账户读模型索引名。</summary>
    public const string PointsAccountIndexName = "leno_points_accounts";

    /// <summary>积分账户标识，作为 ES 文档 _id。</summary>
    public Guid PointsAccountId { get; init; }

    /// <summary>所属用户标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>可用积分余额。</summary>
    public int Balance { get; init; }

    /// <summary>冻结积分余额（下单预占未核销）。</summary>
    public int FrozenAmount { get; init; }

    /// <summary>累计获取积分。</summary>
    public int TotalEarned { get; init; }

    /// <summary>累计消耗积分。</summary>
    public int TotalSpent { get; init; }

    /// <summary>最近一次余额变更时间（UTC），创建事件触发时可为空。</summary>
    public DateTime? LastAdjustedAt { get; init; }

    /// <summary>账户状态名称（Active，预留扩展）。</summary>
    public string Status { get; init; } = "Active";

    /// <summary>索引时间（UTC）。</summary>
    public DateTime IndexedAt { get; init; }

    /// <summary>
    /// 读模型模式版本号，用于后续字段演进时消费方按版本路由反序列化逻辑。
    /// </summary>
    public int SchemaVersion { get; init; } = 1;
}
