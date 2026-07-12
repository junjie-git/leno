using Leno.Infrastructure.Outbox;
using Leno.SellerShop.Domain.Events;
using Leno.SellerShop.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Leno.SellerShop.Infrastructure.BackgroundServices;

/// <summary>
/// 资质到期提醒后台服务，每日扫描即将到期的已通过资质（30/7/1 天），
/// 发布 <see cref="QualificationExpiringEvent"/> 集成事件经发件箱供通知域消费。
/// </summary>
public sealed class QualificationExpiryReminder : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<QualificationExpiryReminder> _logger;
    private static readonly int[] ReminderDays = [30, 7, 1];

    public QualificationExpiryReminder(
        IServiceScopeFactory scopeFactory,
        ILogger<QualificationExpiryReminder> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("资质到期提醒服务已启动");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ScanExpiringQualificationsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "资质到期扫描异常");
            }

            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }

    private async Task ScanExpiringQualificationsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SellerShopDbContext>();

        var utcNow = DateTime.UtcNow;

        foreach (var days in ReminderDays)
        {
            var targetDate = utcNow.AddDays(days);
            var startOfDay = targetDate.Date;
            var endOfDay = startOfDay.AddDays(1);

            var expiringQualifications = await dbContext.ShopQualifications
                .Where(q => q.Status == QualificationStatus.Approved
                            && q.ValidTo >= startOfDay
                            && q.ValidTo < endOfDay)
                .ToListAsync(ct);

            if (expiringQualifications.Count == 0)
            {
                continue;
            }

            var shopIds = expiringQualifications.Select(q => q.ShopId).Distinct().ToList();
            var shops = await dbContext.Shops
                .Where(s => shopIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, ct);

            foreach (var qualification in expiringQualifications)
            {
                if (!shops.TryGetValue(qualification.ShopId, out var shop))
                {
                    _logger.LogWarning("资质 {QualificationId} 关联的店铺 {ShopId} 不存在",
                        qualification.Id, qualification.ShopId);
                    continue;
                }

                var domainEvent = new QualificationExpiringEvent(
                    qualification.Id,
                    qualification.ShopId,
                    shop.SellerId,
                    qualification.Type.ToString(),
                    qualification.Number,
                    qualification.ValidTo,
                    days);

                var outboxMessage = OutboxMessage.Create(domainEvent);
                dbContext.OutboxMessages.Add(outboxMessage);

                _logger.LogInformation(
                    "资质到期提醒: 店铺 {ShopId} 的 {QualificationType} 资质 {Number} 将在 {Days} 天后到期 ({ExpiryDate:yyyy-MM-dd})",
                    qualification.ShopId, qualification.Type, qualification.Number, days, qualification.ValidTo);
            }
        }

        await dbContext.SaveChangesAsync(ct);
    }
}