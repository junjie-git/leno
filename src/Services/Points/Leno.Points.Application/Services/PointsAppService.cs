using Leno.Points.Application.DTOs;
using Leno.Points.Domain.Aggregates.PointsAccount;
using Leno.Points.Domain.Aggregates.PointsExchange;
using Leno.Points.Domain.Exceptions;
using Leno.Points.Domain.Repositories;
using Leno.Points.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using PointsAccountAggregate = Leno.Points.Domain.Aggregates.PointsAccount.PointsAccount;
using PointsExchangeAggregate = Leno.Points.Domain.Aggregates.PointsExchange.PointsExchange;
using PointsFlowAggregate = Leno.Points.Domain.Aggregates.PointsFlow.PointsFlow;

namespace Leno.Points.Application.Services;

/// <summary>
/// 积分应用服务实现，编排积分账户查询、流水查询与兑换用例。
/// </summary>
public sealed class PointsAppService : IPointsAppService
{
    private readonly IPointsAccountRepository _accountRepository;
    private readonly IPointsExchangeRepository _exchangeRepository;
    private readonly IUnitOfWork _unitOfWork;

    public PointsAppService(
        IPointsAccountRepository accountRepository,
        IPointsExchangeRepository exchangeRepository,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(accountRepository);
        ArgumentNullException.ThrowIfNull(exchangeRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        _accountRepository = accountRepository;
        _exchangeRepository = exchangeRepository;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task<PointsAccountDto> GetAccountAsync(Guid userId, CancellationToken ct = default)
    {
        var account = await _accountRepository.GetByUserIdAsync(userId, ct);
        if (account is null)
        {
            account = PointsAccountAggregate.Create(Guid.NewGuid(), userId);
            await _accountRepository.AddAsync(account, ct);
            await _unitOfWork.SaveEntitiesAsync(ct);
        }
        return PointsAccountDto.From(account);
    }

    /// <inheritdoc />
    public async Task<PointsAccountDto> EarnAsync(Guid userId, PointsSource source, int amount, string reason, CancellationToken ct = default)
    {
        var account = await RequireAccountAsync(userId, ct);
        account.Earn(source, amount, reason);
        await _unitOfWork.SaveEntitiesAsync(ct);
        return PointsAccountDto.From(account);
    }

    /// <inheritdoc />
    public async Task<List<PointsFlowDto>> GetLedgerAsync(Guid userId, int page, int pageSize, CancellationToken ct = default)
    {
        // 分页参数边界保护：page < 1 视为第 1 页，pageSize < 1 默认 20，pageSize > 100 上限 100
        if (page < 1)
        {
            page = 1;
        }
        if (pageSize < 1)
        {
            pageSize = 20;
        }
        if (pageSize > 100)
        {
            pageSize = 100;
        }

        var flows = await _accountRepository.GetFlowsByUserIdAsync(userId, page, pageSize, ct);
        return (flows ?? new List<PointsFlowAggregate>()).Select(ToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<Guid> RequestExchangeAsync(ExchangePointsRequestDto request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var account = await RequireAccountAsync(request.UserId, ct);
        var exchangeId = Guid.NewGuid();

        // 创建兑换聚合（初始状态 Pending）
        var exchange = PointsExchangeAggregate.Create(
            exchangeId,
            request.UserId,
            account.Id,
            request.TargetId,
            request.Type,
            request.PointsRequired);

        // 扣减积分（直接消费，不经过冻结流程）
        account.ConsumePoints(request.PointsRequired, exchangeId, $"积分兑换-{request.Type}");

        await _exchangeRepository.AddAsync(exchange, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        return exchangeId;
    }

    /// <inheritdoc />
    public async Task CompleteExchangeAsync(Guid exchangeId, CancellationToken ct = default)
    {
        var exchange = await RequireExchangeAsync(exchangeId, ct);
        exchange.Complete();
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    /// <inheritdoc />
    public async Task FailExchangeAsync(Guid exchangeId, string reason, CancellationToken ct = default)
    {
        var exchange = await RequireExchangeAsync(exchangeId, ct);
        exchange.Fail(reason);

        // 回补积分到账户
        var account = await _accountRepository.GetByUserIdAsync(exchange.UserId, ct);
        if (account is not null)
        {
            account.Earn(
                PointsSource.CouponExchange,
                exchange.PointsRequired,
                $"兑换失败回补-{reason}");
        }

        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    private async Task<PointsAccountAggregate> RequireAccountAsync(Guid userId, CancellationToken ct)
        => await _accountRepository.GetByUserIdAsync(userId, ct)
           ?? throw new PointsDomainException(
               $"用户 {userId} 的积分账户不存在",
               "POINTS_ACCOUNT_NOT_FOUND");

    private async Task<PointsExchangeAggregate> RequireExchangeAsync(Guid exchangeId, CancellationToken ct)
        => await _exchangeRepository.GetByIdAsync(exchangeId, ct)
           ?? throw new PointsDomainException(
               $"兑换记录 {exchangeId} 不存在",
               "POINTS_EXCHANGE_NOT_FOUND");

    private static PointsFlowDto ToDto(PointsFlowAggregate flow)
        => new()
        {
            FlowId = flow.Id,
            AccountId = flow.AccountId,
            TxType = flow.TxType,
            Amount = flow.Amount,
            BalanceAfter = flow.BalanceAfter,
            Source = flow.Source,
            ReferenceId = flow.ReferenceId,
            Reason = flow.Reason,
            OccurredAt = flow.OccurredAt
        };
}
