using Leno.Infrastructure.Abstractions;
using Leno.Infrastructure.EventBus;
using Leno.Points.Domain.Repositories;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;

namespace Leno.Points.Infrastructure.Consumers;

/// <summary>
/// 会员等级变更集成事件消费者（跨 BC 协作：Membership BC → Points BC）。
/// Membership BC 在评估会员等级变化后发布 <see cref="MemberLevelChangedIntegrationEvent"/>，
/// Points BC 消费此事件后调用 <c>PointsAccount.GrantLevelBonus</c> 为用户发放等级提升奖励积分。
/// 通过 EventId 幂等去重，仅在新等级 &gt; 旧等级时发放奖励，等级下降不扣回已发放奖励。
/// 奖励规则：每升 1 级奖励 100 积分（可由配置覆盖）。
/// </summary>
public sealed class MemberLevelChangedEventConsumer : IntegrationEventConsumerBase<MemberLevelChangedIntegrationEvent>
{
    /// <summary>默认每升 1 级奖励的积分数量。</summary>
    private const int DefaultBonusPerLevel = 100;

    private readonly IPointsAccountRepository _accountRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly int _bonusPerLevel;

    public MemberLevelChangedEventConsumer(
        IPointsAccountRepository accountRepository,
        IUnitOfWork unitOfWork,
        ILogger<MemberLevelChangedEventConsumer> logger,
        IIdempotencyStore idempotencyStore,
        Microsoft.Extensions.Options.IOptions<PointsBonusOptions>? options = null)
        : base(logger, idempotencyStore)
    {
        _accountRepository = accountRepository;
        _unitOfWork = unitOfWork;
        _bonusPerLevel = options?.Value?.LevelUpBonusPerLevel ?? DefaultBonusPerLevel;
    }

    protected override async Task HandleAsync(MemberLevelChangedIntegrationEvent integrationEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        // 仅在新等级 > 旧等级时发放奖励（升级场景）
        if (integrationEvent.NewLevel <= integrationEvent.OldLevel)
        {
            Logger.LogInformation(
                "会员等级未升级（OldLevel={Old} NewLevel={New}），跳过奖励积分发放 UserId={UserId}",
                integrationEvent.OldLevel, integrationEvent.NewLevel, integrationEvent.UserId);
            return;
        }

        var account = await _accountRepository.GetByUserIdAsync(integrationEvent.UserId, ct);
        if (account is null)
        {
            Logger.LogWarning(
                "用户 {UserId} 积分账户不存在，跳过等级提升奖励积分发放",
                integrationEvent.UserId);
            return;
        }

        var levelDelta = integrationEvent.NewLevel - integrationEvent.OldLevel;
        var bonusAmount = levelDelta * _bonusPerLevel;

        account.GrantLevelBonus(bonusAmount, integrationEvent.NewLevel);

        await _unitOfWork.SaveEntitiesAsync(ct);

        Logger.LogInformation(
            "会员等级升级 UserId={UserId} OldLevel={Old} NewLevel={New}，发放奖励积分 {Bonus}",
            integrationEvent.UserId, integrationEvent.OldLevel, integrationEvent.NewLevel, bonusAmount);
    }
}

/// <summary>
/// 积分奖励配置选项。
/// </summary>
public sealed class PointsBonusOptions
{
    /// <summary>会员等级每升 1 级奖励的积分数量，默认 100。</summary>
    public int LevelUpBonusPerLevel { get; set; } = 100;
}
