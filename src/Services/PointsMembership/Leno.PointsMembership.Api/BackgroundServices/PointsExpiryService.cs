using Leno.PointsMembership.Application;
using Leno.PointsMembership.Domain.Aggregates;
using Leno.PointsMembership.Domain.Repositories;
using Leno.PointsMembership.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.PointsMembership.Api.BackgroundServices;

/// <summary>
/// 积分过期处理后台服务，每天扫描积分账户，按 FIFO 原则查找过期积分并标记清理。
/// 批次大小：500。
/// </summary>
public sealed class PointsExpiryService : BackgroundService
{
    private const int BatchSize = 500;
    private static readonly TimeSpan ScanInterval = TimeSpan.FromDays(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PointsExpiryService> _logger;
    private readonly PointsMembershipOptions _options;

    public PointsExpiryService(
        IServiceScopeFactory scopeFactory,
        ILogger<PointsExpiryService> logger,
        IOptions<PointsMembershipOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PointsExpiryService 启动，扫描间隔 {Interval}，过期阈值 {Months} 个月",
            ScanInterval, _options.ExpiryMonths);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessExpiredPointsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "积分过期处理异常");
            }

            await Task.Delay(ScanInterval, stoppingToken);
        }
    }

    private async Task ProcessExpiredPointsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var accountRepository = scope.ServiceProvider.GetRequiredService<IPointsAccountRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var expiryThreshold = DateTime.UtcNow.AddMonths(-_options.ExpiryMonths);
        var totalAccounts = 0;
        var totalExpired = 0;
        var skip = 0;

        while (!ct.IsCancellationRequested)
        {
            var accounts = await accountRepository.GetAllWithPositiveBalanceAsync(skip, BatchSize, ct);

            if (accounts.Count == 0)
            {
                break;
            }

            foreach (var account in accounts)
            {
                var expiredPoints = await CalculateExpiredPointsAsync(
                    account, expiryThreshold, accountRepository, ct);

                if (expiredPoints > 0)
                {
                    account.ExpirePoints(expiredPoints);
                    await accountRepository.UpdateAsync(account, ct);
                    totalExpired += expiredPoints;

                    _logger.LogDebug(
                        "用户 {UserId} 积分过期 {Points} 分，剩余 {Balance} 分",
                        account.UserId, expiredPoints, account.Balance);
                }
            }

            await unitOfWork.SaveEntitiesAsync(ct);
            totalAccounts += accounts.Count;
            skip += BatchSize;
        }

        if (totalExpired > 0)
        {
            _logger.LogInformation(
                "积分过期处理完成，共处理 {Accounts} 个账户，过期 {Points} 积分",
                totalAccounts, totalExpired);
        }
    }

    /// <summary>
    /// 按 FIFO 原则计算账户中应过期的积分数。
    /// 遍历按时间升序排列的 Earn 流水，将超过过期阈值的积分累加，但不超过当前可用余额。
    /// </summary>
    private async Task<int> CalculateExpiredPointsAsync(
        PointsAccount account,
        DateTime expiryThreshold,
        IPointsAccountRepository accountRepository,
        CancellationToken ct)
    {
        if (account.Balance <= 0)
        {
            return 0;
        }

        var earnLedgers = await accountRepository.GetEarnLedgersByAccountIdAsync(account.Id, ct);

        var expiredPoints = 0;
        var remainingBalance = account.Balance;

        foreach (var ledger in earnLedgers)
        {
            if (ledger.OccurredAt >= expiryThreshold)
            {
                break; // Reached non-expired entries (sorted by OccurredAt ascending)
            }

            if (remainingBalance <= 0)
            {
                break;
            }

            // FIFO: expire the minimum of the ledger amount and remaining balance
            var toExpire = Math.Min(ledger.Amount, remainingBalance);
            expiredPoints += toExpire;
            remainingBalance -= toExpire;
        }

        return expiredPoints;
    }
}