using Leno.Infrastructure.EventBus;
using Leno.PointsMembership.Domain.Aggregates;
using Leno.PointsMembership.Domain.Repositories;
using Leno.PointsMembership.Domain.ValueObjects;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Leno.Infrastructure.Abstractions;

namespace Leno.PointsMembership.Infrastructure.Consumers;

/// <summary>
/// 订单支付成功事件消费者。
/// 确认积分扣减（ConfirmDeduct）；若为会员订阅订单则激活 UserMembership。
/// 通过 EventId 幂等去重。
/// PM-M04 修复：捕获 DbUpdateConcurrencyException 视为已处理（并发更新已被另一实例完成），避免无意义重试。
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
        ILogger<OrderPaidEventConsumer> logger,
        IIdempotencyStore idempotencyStore)
        : base(logger, idempotencyStore)
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
        else
        {
            Logger.LogInformation("订单 {OrderId} 无冻结积分，跳过 ConfirmDeduct", integrationEvent.OrderId);
        }

        // 2. 若为会员订阅订单，激活 UserMembership
        // PM-H08 修复：package null 或 DurationDays<=0 时记录告警并跳过 Activate，
        // 避免 UserMembership.Activate 抛 MEMBERSHIP_DURATION_INVALID 触发 MassTransit 重试死循环
        // （ConfirmDeduct 已成功执行，重试会触发 POINTS_FROZEN_ENTRY_NOT_FOUND 死循环）
        var userMembership = await _userMembershipRepository.GetByOrderIdAsync(integrationEvent.OrderId, ct);
        if (userMembership is not null && userMembership.Status == UserMembershipStatus.Pending)
        {
            var package = await _packageRepository.GetByIdAsync(userMembership.PackageId, ct);
            if (package is null)
            {
                Logger.LogWarning(
                    "会员订阅订单 {OrderId} 对应套餐 {PackageId} 不存在或已下架，跳过 UserMembership 激活，需人工处理",
                    integrationEvent.OrderId, userMembership.PackageId);
            }
            else if (package.DurationDays <= 0)
            {
                Logger.LogWarning(
                    "会员订阅订单 {OrderId} 对应套餐 {PackageId} DurationDays={Days} 异常，跳过 UserMembership 激活，需人工处理",
                    integrationEvent.OrderId, userMembership.PackageId, package.DurationDays);
            }
            else
            {
                userMembership.Activate(integrationEvent.OrderId, integrationEvent.PaidAt, package.DurationDays);
                Logger.LogInformation("会员订阅订单 {OrderId} 支付成功，已激活会员 {UserMembershipId}",
                    integrationEvent.OrderId, userMembership.Id);
            }
        }

        // PM-M04 修复：捕获 DbUpdateConcurrencyException，视为并发更新已被另一实例完成，记录日志并正常返回
        // 避免乐观锁冲突触发 MassTransit 无意义重试（重复事件场景下另一消费者已成功激活）
        try
        {
            await _unitOfWork.SaveEntitiesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            Logger.LogWarning(ex,
                "订单 {OrderId} 保存时发生乐观锁冲突，视为并发实例已处理，跳过重试",
                integrationEvent.OrderId);
        }
    }
}
