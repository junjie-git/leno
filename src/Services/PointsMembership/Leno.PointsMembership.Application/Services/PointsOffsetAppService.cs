using Leno.PointsMembership.Domain.Exceptions;
using Leno.PointsMembership.Domain.Repositories;
using Leno.PointsMembership.Domain.Services;
using Leno.SharedKernel.Abstractions;

namespace Leno.PointsMembership.Application.Services;

/// <summary>
/// 积分抵扣防腐层实现，供订单域调用以试算、冻结、确认扣减与释放积分。
/// 通过积分账户仓储操作聚合根，经工作单元在同事务内保存变更与发件箱事件。
/// 抵扣换算：100 积分 = 1 元。
/// </summary>
public sealed class PointsOffsetAppService : IPointsOffsetService
{
    private const int PointsPerYuan = 100;

    private readonly IPointsAccountRepository _accountRepository;
    private readonly IUnitOfWork _unitOfWork;

    public PointsOffsetAppService(
        IPointsAccountRepository accountRepository,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(accountRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        _accountRepository = accountRepository;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task<decimal> TryOffsetAsync(Guid userId, int pointsToUse, CancellationToken ct = default)
    {
        if (pointsToUse <= 0)
        {
            return 0;
        }

        var account = await _accountRepository.GetByUserIdAsync(userId, ct);
        if (account is null || account.Balance < pointsToUse)
        {
            return 0;
        }

        return pointsToUse / (decimal)PointsPerYuan;
    }

    /// <inheritdoc />
    public async Task FreezeAsync(Guid userId, Guid orderId, int pointsToUse, CancellationToken ct = default)
    {
        var account = await _accountRepository.GetByUserIdAsync(userId, ct)
            ?? throw new PointsDomainException(
                $"用户 {userId} 的积分账户不存在",
                "POINTS_ACCOUNT_NOT_FOUND",
                404);

        account.Freeze(pointsToUse, orderId);
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    /// <inheritdoc />
    public async Task ConfirmDeductAsync(Guid orderId, CancellationToken ct = default)
    {
        var account = await _accountRepository.GetByFrozenOrderIdAsync(orderId, ct)
            ?? throw new PointsDomainException(
                $"订单 {orderId} 的冻结记录不存在",
                "POINTS_FROZEN_ENTRY_NOT_FOUND",
                404);

        account.ConfirmDeduct(orderId);
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    /// <inheritdoc />
    public async Task ReleaseAsync(Guid orderId, CancellationToken ct = default)
    {
        var account = await _accountRepository.GetByFrozenOrderIdAsync(orderId, ct)
            ?? throw new PointsDomainException(
                $"订单 {orderId} 的冻结记录不存在",
                "POINTS_FROZEN_ENTRY_NOT_FOUND",
                404);

        account.Release(orderId);
        await _unitOfWork.SaveEntitiesAsync(ct);
    }
}
