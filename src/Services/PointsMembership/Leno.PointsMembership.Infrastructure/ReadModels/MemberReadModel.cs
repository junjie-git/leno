namespace Leno.PointsMembership.Infrastructure.ReadModels;

/// <summary>
/// 会员 ES 读模型文档，索引名 <see cref="MemberIndexName"/>。
/// 用于用户端会员信息查询与运营分析的快速检索。
/// 写侧会员档案创建时由 <see cref="MemberRegisteredReadModelSyncConsumer"/> 索引；
/// 会员等级升级时由 <see cref="MemberLevelUpgradedReadModelSyncConsumer"/> 重建（IndexAsync 覆盖更新）。
/// </summary>
public sealed class MemberReadModel
{
    /// <summary>会员读模型索引名。</summary>
    public const string MemberIndexName = "leno_members";

    /// <summary>会员标识，作为 ES 文档 _id。</summary>
    public Guid MemberId { get; init; }

    /// <summary>所属用户标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>当前会员等级编号（基于消费的等级体系）。</summary>
    public int Level { get; init; }

    /// <summary>累计消费金额。</summary>
    public decimal TotalConsumption { get; init; }

    /// <summary>当前成长值（基于消费积分累计）。</summary>
    public int GrowthValue { get; init; }

    /// <summary>当前成长值等级编号（V0-V4）。</summary>
    public int GrowthLevel { get; init; }

    /// <summary>会员档案创建时间（UTC）。</summary>
    public DateTime RegisteredAt { get; init; }

    /// <summary>最近一次等级升级时间（UTC）。</summary>
    public DateTime LastUpgradeAt { get; init; }

    /// <summary>会员状态名称（Active/Frozen）。</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>索引时间（UTC）。</summary>
    public DateTime IndexedAt { get; init; }

    /// <summary>
    /// 读模型模式版本号，用于后续字段演进时消费方按版本路由反序列化逻辑。
    /// </summary>
    public int SchemaVersion { get; init; } = 1;
}
