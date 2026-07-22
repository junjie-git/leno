using Leno.Infrastructure.Outbox;
using Leno.SharedContracts.Events;
using Leno.SellerShop.Domain.ValueObjects;
using Leno.SellerShop.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.SellerShop.Infrastructure.BackgroundServices;

/// <summary>
/// 资质到期提醒后台服务，按配置的扫描间隔检查即将到期的已通过资质，
/// 在距到期日 <see cref="QualificationReminderOptions.ReminderDays"/> 天时发布
/// <see cref="QualificationExpiringIntegrationEvent"/> 集成事件经发件箱供通知域消费。
/// </summary>
public sealed class QualificationExpiryReminder : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<QualificationExpiryReminder> _logger;
    private readonly QualificationReminderOptions _options;

    public QualificationExpiryReminder(
        IServiceScopeFactory scopeFactory,
        ILogger<QualificationExpiryReminder> logger,
        IOptions<QualificationReminderOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalHours = _options.ScanIntervalHours > 0 ? _options.ScanIntervalHours : 24;
        _logger.LogInformation("资质到期提醒服务已启动，扫描间隔 {IntervalHours} 小时，提醒天数 {ReminderDays}",
            intervalHours, string.Join(",", _options.ReminderDays));

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

            await Task.Delay(TimeSpan.FromHours(intervalHours), stoppingToken);
        }
    }

    private async Task ScanExpiringQualificationsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SellerShopDbContext>();

        var utcNow = DateTime.UtcNow;

        foreach (var days in _options.ReminderDays)
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

                var integrationEvent = new QualificationExpiringIntegrationEvent(
                    qualification.Id,
                    qualification.ShopId,
                    shop.SellerId,
                    qualification.Type.ToString(),
                    qualification.Number,
                    qualification.ValidTo,
                    days);

                var outboxMessage = OutboxMessage.Create(integrationEvent);
                dbContext.OutboxMessages.Add(outboxMessage);

                _logger.LogInformation(
                    "资质到期提醒: 店铺 {ShopId} 的 {QualificationType} 资质 {Number} 将在 {Days} 天后到期 ({ExpiryDate:yyyy-MM-dd})",
                    qualification.ShopId, qualification.Type, qualification.Number, days, qualification.ValidTo);
            }
        }

        await dbContext.SaveChangesAsync(ct);
    }
}