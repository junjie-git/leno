using Leno.Infrastructure.Abstractions;
using Leno.Infrastructure.EventBus;
using Leno.Membership.Domain.Repositories;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;

namespace Leno.Membership.Infrastructure.Consumers;

/// <summary>
/// 积分入账集成事件消费者（跨 BC 协作：Points BC → Membership BC）。
/// Points BC 在积分入账时发布 <see cref="PointsEarnedIntegrationEvent"/>，
/// Membership BC 消费此事件后调用 <c>Member.AddGrowthValue</c> 累加成长值（1 积分 = 1 成长值）。
/// 通过 EventId 幂等去重，积分来源为 MemberLevelBonus 时跳过（避免成长值与等级奖励积分循环累加）。
/// </summary>
public sealed class PointsEarnedEventConsumer : IntegrationEventConsumerBase<PointsEarnedIntegrationEvent>
{
    /// <summary>会员等级提升奖励积分来源标识，消费时跳过此来源，避免循环累加。</summary>
    private const string MemberLevelBonusSource = "MemberLevelBonus";

    private readonly IMemberRepository _memberRepository;
    private readonly IUnitOfWork _unitOfWork;

    public PointsEarnedEventConsumer(
        IMemberRepository memberRepository,
        IUnitOfWork unitOfWork,
        ILogger<PointsEarnedEventConsumer> logger,
        IIdempotencyStore idempotencyStore)
        : base(logger, idempotencyStore)
    {
        _memberRepository = memberRepository;
        _unitOfWork = unitOfWork;
    }

    protected override async Task HandleAsync(PointsEarnedIntegrationEvent integrationEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        // 跳过等级奖励积分来源，避免循环：Points BC 发放等级奖励积分 → Membership BC 累加成长值 → 等级提升 → 再次发放奖励
        if (string.Equals(integrationEvent.Source, MemberLevelBonusSource, StringComparison.OrdinalIgnoreCase))
        {
            Logger.LogDebug(
                "跳过等级奖励积分来源的成长值累加 UserId={UserId} Source={Source}",
                integrationEvent.UserId, integrationEvent.Source);
            return;
        }

        var member = await _memberRepository.GetByUserIdAsync(integrationEvent.UserId, ct);
        if (member is null)
        {
            // 会员档案不存在时静默跳过：可能用户尚未注册会员档案（延迟创建场景）
            Logger.LogWarning(
                "用户 {UserId} 会员档案不存在，跳过成长值累加（积分来源 {Source}，数量 {Amount}）",
                integrationEvent.UserId, integrationEvent.Source, integrationEvent.Amount);
            return;
        }

        var reason = $"积分入账-来源{integrationEvent.Source}";
        member.AddGrowthValue(integrationEvent.Amount, reason);

        await _unitOfWork.SaveEntitiesAsync(ct);

        Logger.LogInformation(
            "积分入账累加成长值 UserId={UserId} Amount={Amount} Source={Source}，当前成长值 {GrowthValue}",
            integrationEvent.UserId, integrationEvent.Amount, integrationEvent.Source, member.GrowthValue);
    }
}
