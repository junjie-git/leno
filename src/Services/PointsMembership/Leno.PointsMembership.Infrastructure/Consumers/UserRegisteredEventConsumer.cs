using Leno.Infrastructure.EventBus;
using Leno.PointsMembership.Domain.Aggregates;
using Leno.PointsMembership.Domain.Repositories;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;

namespace Leno.PointsMembership.Infrastructure.Consumers;

/// <summary>
/// 用户注册事件消费者，自动创建积分账户与会员档案。
/// 通过 EventId 幂等去重。
/// </summary>
public sealed class UserRegisteredEventConsumer : IntegrationEventConsumerBase<UserRegisteredEvent>
{
    private readonly IPointsAccountRepository _accountRepository;
    private readonly IMemberRepository _memberRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UserRegisteredEventConsumer(
        IPointsAccountRepository accountRepository,
        IMemberRepository memberRepository,
        IUnitOfWork unitOfWork,
        ILogger<UserRegisteredEventConsumer> logger)
        : base(logger)
    {
        _accountRepository = accountRepository;
        _memberRepository = memberRepository;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    protected override async Task HandleAsync(UserRegisteredEvent integrationEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        // 幂等：若账户已存在则跳过
        var existingAccount = await _accountRepository.GetByUserIdAsync(integrationEvent.UserId, ct);
        if (existingAccount is not null)
        {
            Logger.LogInformation("用户 {UserId} 积分账户已存在，跳过创建", integrationEvent.UserId);
            return;
        }

        var account = PointsAccount.Create(Guid.NewGuid(), integrationEvent.UserId);
        await _accountRepository.AddAsync(account, ct);

        var member = Member.Create(Guid.NewGuid(), integrationEvent.UserId);
        await _memberRepository.AddAsync(member, ct);

        await _unitOfWork.SaveEntitiesAsync(ct);

        Logger.LogInformation("用户 {UserId} 注册，已创建积分账户 {AccountId} 与会员档案 {MemberId}",
            integrationEvent.UserId, account.Id, member.Id);
    }
}
