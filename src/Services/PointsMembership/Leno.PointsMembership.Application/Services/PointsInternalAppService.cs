using Leno.PointsMembership.Domain.Exceptions;
using Leno.PointsMembership.Domain.Repositories;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using PointsAccountAggregate = Leno.PointsMembership.Domain.Aggregates.PointsAccount;

namespace Leno.PointsMembership.Application.Services;

/// <summary>
/// 积分域内部操作服务实现，供订单域调用以试扣、冻结、释放积分。
/// 通过积分账户仓储操作聚合根，经工作单元在同事务内保存变更与发件箱事件。
/// </summary>
public sealed class PointsInternalAppService : IPointsInternalAppService
{
    private readonly IPointsAccountRepository _accountRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PointsInternalAppService> _logger;

    public PointsInternalAppService(
        IPointsAccountRepository accountRepository,
        IUnitOfWork unitOfWork,
        ILogger<PointsInternalAppService> logger)
    {
        ArgumentNullException.ThrowIfNull(accountRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(logger);
        _accountRepository = accountRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<TrialOffsetResultDto> TrialOffsetAsync(TrialOffsetDto input, CancellationToken ct = default)
    {
        var account = await _accountRepository.GetByUserIdAsync(input.UserId, ct);
        if (account is null)
        {
            return new TrialOffsetResultDto { OffsetAmount = 0 };
        }

        var offsetAmount = account.TryOffset(input.PointsToUse);
        return new TrialOffsetResultDto { OffsetAmount = offsetAmount };
    }

    /// <inheritdoc />
    public async Task FreezeAsync(FreezePointsDto input, CancellationToken ct = default)
    {
        var account = await _accountRepository.GetByUserIdAsync(input.UserId, ct)
            ?? throw new PointsDomainException(
                $"用户 {input.UserId} 的积分账户不存在",
                "POINTS_ACCOUNT_NOT_FOUND");

        account.Freeze(input.PointsToUse, input.OrderId);
        await _unitOfWork.SaveEntitiesAsync(ct);
        _logger.LogInformation(
            "积分冻结成功 UserId={UserId} OrderId={OrderId} Points={Points}",
            input.UserId, input.OrderId, input.PointsToUse);
    }

    /// <inheritdoc />
    public async Task ReleaseAsync(ReleasePointsDto input, CancellationToken ct = default)
    {
        var account = await _accountRepository.GetByFrozenOrderIdAsync(input.OrderId, ct)
            ?? throw new PointsDomainException(
                $"订单 {input.OrderId} 的冻结记录不存在",
                "POINTS_FROZEN_ENTRY_NOT_FOUND");

        account.Release(input.OrderId);
        await _unitOfWork.SaveEntitiesAsync(ct);
        _logger.LogInformation("积分释放成功 OrderId={OrderId}", input.OrderId);
    }

    /// <inheritdoc />
    public async Task ConfirmAsync(ConfirmPointsDto input, CancellationToken ct = default)
    {
        var account = await _accountRepository.GetByFrozenOrderIdAsync(input.OrderId, ct)
            ?? throw new PointsDomainException(
                $"订单 {input.OrderId} 的冻结记录不存在",
                "POINTS_FROZEN_ENTRY_NOT_FOUND");

        account.ConfirmDeduct(input.OrderId);
        await _unitOfWork.SaveEntitiesAsync(ct);
        _logger.LogInformation("积分确认扣减成功 OrderId={OrderId}", input.OrderId);
    }
}
