using Leno.Promotion.Domain.Repositories;
using Leno.Promotion.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;

namespace Leno.Promotion.Api.BackgroundServices;

/// <summary>
/// 优惠券过期处理后台服务，定时扫描已领取但未使用且已过期的用户券，批量标记为 Expired。
/// 扫描频率：每 1 小时，批次大小：500。
/// </summary>
public sealed class CouponExpiryService : BackgroundService
{
    private const int BatchSize = 500;
    private static readonly TimeSpan ScanInterval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CouponExpiryService> _logger;

    public CouponExpiryService(
        IServiceScopeFactory scopeFactory,
        ILogger<CouponExpiryService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("CouponExpiryService 启动，扫描间隔 {Interval}", ScanInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessExpiredCouponsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "优惠券过期处理异常");
            }

            await Task.Delay(ScanInterval, stoppingToken);
        }
    }

    private async Task ProcessExpiredCouponsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var userCouponRepository = scope.ServiceProvider.GetRequiredService<IUserCouponRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var totalExpired = 0;
        var skip = 0;

        while (!ct.IsCancellationRequested)
        {
            var batch = await userCouponRepository.GetExpiredUnusedCouponsAsync(skip, BatchSize, ct);

            if (batch.Count == 0)
            {
                break;
            }

            foreach (var userCoupon in batch)
            {
                userCoupon.Expire();
                await userCouponRepository.UpdateAsync(userCoupon, ct);
            }

            await unitOfWork.SaveEntitiesAsync(ct);
            totalExpired += batch.Count;
            skip += BatchSize;

            _logger.LogDebug("已处理一批过期优惠券，本批 {Count} 张，累计 {Total} 张", batch.Count, totalExpired);
        }

        if (totalExpired > 0)
        {
            _logger.LogInformation("优惠券过期处理完成，共标记过期 {Total} 张", totalExpired);
        }
    }
}