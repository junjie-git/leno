using Leno.Infrastructure.ReadModel;
using Leno.PointsMembership.Domain.Repositories;
using Leno.SharedContracts.Events;
using Microsoft.Extensions.Logging;

namespace Leno.PointsMembership.Infrastructure.ReadModels;

/// <summary>
/// 会员等级升级读模型同步消费者：消费 <see cref="MemberLevelUpgradedEvent"/>，
/// 注入 <see cref="IMemberRepository"/> 查询最新会员聚合根，重建 <see cref="MemberReadModel"/>
/// 并通过 IndexAsync 覆盖更新到 Elasticsearch（不删除）。
/// 索引失败抛出异常以触发 MassTransit 重试与死信队列；聚合根不存在时跳过同步。
/// 幂等：ES 索引以会员标识为 _id，重复索引为覆盖更新。
/// </summary>
public sealed class MemberLevelUpgradedReadModelSyncConsumer
    : ReadModelSyncConsumerBase<MemberLevelUpgradedEvent, MemberReadModel>
{
    private readonly IMemberRepository _memberRepository;

    public MemberLevelUpgradedReadModelSyncConsumer(
        IEsReadModelRepository<MemberReadModel> repository,
        IMemberRepository memberRepository,
        ILogger<MemberLevelUpgradedReadModelSyncConsumer> logger)
        : base(repository, logger)
    {
        _memberRepository = memberRepository;
    }

    /// <inheritdoc />
    /// <remarks>等级升级事件触发索引重建（按最新聚合根快照），不触发删除。</remarks>
    protected override async Task<(string Id, string IndexName, MemberReadModel? ReadModel)> BuildReadModelAsync(
        MemberLevelUpgradedEvent integrationEvent, CancellationToken ct)
    {
        var member = await _memberRepository.GetByIdAsync(integrationEvent.MemberId, ct);
        if (member is null)
        {
            Logger.LogWarning("会员 {MemberId} 不存在，跳过读模型同步", integrationEvent.MemberId);
            return (string.Empty, string.Empty, null);
        }

        var readModel = new MemberReadModel
        {
            MemberId = member.Id,
            UserId = member.UserId,
            Level = member.CurrentLevel,
            TotalConsumption = member.TotalConsumption,
            GrowthValue = member.GrowthValue,
            GrowthLevel = member.CurrentGrowthLevel,
            RegisteredAt = member.JoinedAt,
            LastUpgradeAt = member.LevelUpgradedAt,
            Status = member.Status.ToString(),
            IndexedAt = DateTime.UtcNow,
            SchemaVersion = 1
        };

        return (member.Id.ToString(), MemberReadModel.MemberIndexName, readModel);
    }

    /// <inheritdoc />
    /// <remarks>等级升级事件仅触发索引重建，不删除读模型。</remarks>
    protected override Task<(string Id, string IndexName)?> BuildDeleteActionAsync(
        MemberLevelUpgradedEvent integrationEvent, CancellationToken ct)
        => Task.FromResult<(string, string)?>(null);
}
