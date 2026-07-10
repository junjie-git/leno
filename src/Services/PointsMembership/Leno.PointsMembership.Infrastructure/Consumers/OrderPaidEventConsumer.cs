using Leno.Infrastructure.EventBus;
using Leno.PointsMembership.Domain.Aggregates;
using Leno.PointsMembership.Domain.Repositories;
using Leno.PointsMembership.Domain.ValueObjects;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;

namespace Leno.PointsMembership.Infrastructure.Consumers;

/// <summary>
/// 订单支付成功事件消费者。
/// 确认积分扣减（ConfirmDeduct）；若为会员订阅订单则激活 UserMembership。
/// 通过 EventId 幂等去重。
/// </summary>
public sealed class OrderPaidEventConsumer : IntegrationEventConsumerBase<OrderPaidEvent>
{
    private readonly IPointsAccountRepository _accountRepository;
    private readonly IUserMembershipRepository _userMembershipRepository;
    private readonly IMembershipPackageRepository _packageRepository;
    private readonly IUnitOfWork _unitOfWork;

    public OrderPaidEventConsumer(
        IPointsAccountRepository accountRepository,
        IUserMembershipRepository userMembershipRepository,
        IMembershipPackageRepository packageRepository,
        IUnitOfWork unitOfWork,
        ILogger<OrderPaidEventConsumer> logger)
        : base(logger)
    {
        _accountRepository = accountRepository;
        _userMembershipRepository = userMembershipRepository;
        _packageRepository = packageRepository;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    protected override async Task HandleAsync(OrderPaidEvent integrationEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        // 1. 确认积分扣减（若该订单冻结了积分）
        var account = await _accountRepository.GetByFrozenOrderIdAsync(integrationEvent.OrderId, ct);
        if (account is not null)
        {
            account.ConfirmDeduct(integrationEvent.OrderId);
            Logger.LogInformation("订单 {OrderId} 支付成功，已确认扣减积分", integrationEvent.OrderId);
        }

        // 2. 若为会员订阅订单，激活 UserMembership
        var userMembership = await _userMembershipRepository.GetByOrderIdAsync(integrationEvent.OrderId, ct);
        if (userMembership is not null && userMembership.Status == UserMembershipStatus.Pending)
        {
            var package = await _packageRepository.GetByIdAsync(userMembership.PackageId, ct);
            var durationDays = package?.DurationDays ?? 0;
            userMembership.Activate(integrationEvent.OrderId, integrationEvent.PaidAt, durationDays);
            Logger.LogInformation("会员订阅订单 {OrderId} 支付成功，已激活会员 {UserMembershipId}",
                integrationEvent.OrderId, userMembership.Id);
        }

        await _unitOfWork.SaveEntitiesAsync(ct);
    }
}
