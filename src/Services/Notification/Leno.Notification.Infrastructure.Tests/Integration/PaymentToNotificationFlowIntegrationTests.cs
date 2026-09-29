using FluentAssertions;
using Leno.Infrastructure.Persistence;
using Leno.Notification.Application.Services;
using Leno.Notification.Domain.Aggregates;
using Leno.Notification.Domain.Repositories;
using Leno.Notification.Domain.Services;
using Leno.Notification.Domain.ValueObjects;
using Leno.Notification.Infrastructure;
using Leno.Notification.Infrastructure.Channels;
using Leno.Notification.Infrastructure.Consumers;
using Leno.Notification.Infrastructure.Repositories;
using Leno.Notification.Infrastructure.Services;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Leno.Testing.Fixtures;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Leno.Notification.Infrastructure.Tests.Integration;

/// <summary>
/// F8 核心链路段③：支付成功 → 通知发送（跨 BC 真实集成）。
/// 链路：PaymentSucceededEvent（真实 RabbitMQ TestHarness）
///     → PaymentEventConsumer（真实，EventId 下推为幂等键）
///     → NotificationService.SendAsync（真实：模板加载 → 渲染 → 发送记录落库 → InApp 渠道投递）
///     → NotificationRecord 以 IdempotencyKey=EventId 持久化。
/// 事件重放（同一 EventId）应命中幂等去重，不产生第二条记录。
/// 渠道矩阵仅注册 InApp（真实 Redis 投递）；Sms/Email 渠道需外部服务商凭据，不在本链路范围。
/// </summary>
public class PaymentToNotificationFlowIntegrationTests : CrossBcIntegrationTestBase<NotificationDbContext>
{
    private const string TemplateCode = "payment_succeeded";

    public PaymentToNotificationFlowIntegrationTests(ContainerFixture fixture) : base(fixture)
    {
    }

    protected override void ConfigureServices(IServiceCollection services, string sqlConnectionString, string rabbitMqConnectionString)
    {
        services.AddDbContext<NotificationDbContext>(options => options.UseSqlServer(sqlConnectionString));
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork<NotificationDbContext>>();
        services.AddScoped<INotificationTemplateRepository, EfCoreNotificationTemplateRepository>();
        services.AddScoped<INotificationRecordRepository, EfCoreNotificationRecordRepository>();
        services.AddScoped<ITemplateRenderer, TemplateRenderer>();

        // 渠道矩阵仅注册 InApp（真实实现，投递走真实 Redis）；用户联系方式 ACL 仅
        // 非 InApp 渠道使用，InApp 链路不触达，Mock 即可
        services.AddScoped<INotificationChannel, InAppChannel>();
        services.AddScoped<IUserContactService>(_ => Mock.Of<IUserContactService>());

        services.AddScoped<INotificationService>(sp => new NotificationService(
            sp.GetRequiredService<INotificationTemplateRepository>(),
            sp.GetRequiredService<INotificationRecordRepository>(),
            sp.GetRequiredService<ITemplateRenderer>(),
            sp.GetServices<INotificationChannel>(),
            sp.GetRequiredService<IUserContactService>(),
            sp.GetRequiredService<IUnitOfWork>(),
            sp.GetRequiredService<ILogger<NotificationService>>()));

        services.AddScoped<PaymentEventConsumer>();
    }

    protected override void ConfigureConsumers(IBusRegistrationConfigurator configurator)
    {
        configurator.AddConsumer<PaymentEventConsumer>();
    }

    [Fact]
    public async Task PaymentSucceeded_ShouldPersistInAppRecord_WithEventIdIdempotency()
    {
        // Arrange：种子启用态 InApp 模板（每个用例独立种子，用例间同库共享数据）
        await SeedPaymentTemplateAsync();

        var evt = new PaymentSucceededEvent
        {
            EventId = Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            PaymentId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Channel = "WeChatPay",
            TradeNo = "TEST_TRADE_F8_N1",
            Amount = 199.8m
        };

        // Act：发布支付成功事件（真实总线 → 真实消费者 → 真实通知服务）
        await TestHarness.Bus.Publish(evt);

        using var consumedCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        (await TestHarness.Consumed.Any<PaymentSucceededEvent>(consumedCts.Token))
            .Should().BeTrue("PaymentEventConsumer 应消费支付成功事件");

        // Assert：发送记录以 IdempotencyKey=EventId 落库且发送成功（异步消费，轮询等待）
        var record = await PollUntilAsync(() => GetRecordAsync(evt.EventId.ToString()));

        record!.Status.Should().Be(NotificationStatus.Succeeded, "InApp 渠道投递应成功");
        record.TemplateCode.Should().Be(TemplateCode);
        record.IdempotencyKey.Should().Be(evt.EventId.ToString());

        // Assert：事件重放（同一 EventId）命中幂等去重，不产生第二条记录
        await TestHarness.Bus.Publish(evt);
        await Task.Delay(TimeSpan.FromSeconds(3));

        (await CountRecordsAsync(evt.EventId.ToString())).Should().Be(1, "重放同一事件应命中 IdempotencyKey 去重");
    }

    [Fact]
    public async Task TwoDistinctPaymentEvents_ShouldProduceTwoRecords_WithIsolatedIdempotency()
    {
        // Arrange：同一模板，两个不同支付事件（不同 EventId/Order）
        await SeedPaymentTemplateAsync();

        var event1 = new PaymentSucceededEvent
        {
            EventId = Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            PaymentId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Channel = "WeChatPay",
            TradeNo = "TEST_TRADE_F8_N2A",
            Amount = 88.8m
        };
        var event2 = new PaymentSucceededEvent
        {
            EventId = Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            PaymentId = Guid.NewGuid(),
            UserId = event1.UserId,
            Channel = "WeChatPay",
            TradeNo = "TEST_TRADE_F8_N2B",
            Amount = 66.6m
        };

        // Act
        await TestHarness.Bus.Publish(event1);
        await TestHarness.Bus.Publish(event2);

        using var consumedCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        (await TestHarness.Consumed.Any<PaymentSucceededEvent>(consumedCts.Token))
            .Should().BeTrue("PaymentEventConsumer 应消费支付成功事件");

        // Assert：幂等键彼此隔离 → 两条独立发送记录（去重按键而非全局）
        var r1 = await PollUntilAsync(() => GetRecordAsync(event1.EventId.ToString()));
        var r2 = await PollUntilAsync(() => GetRecordAsync(event2.EventId.ToString()));
        r1.Should().NotBeNull("事件 1 应有独立发送记录");
        r2.Should().NotBeNull("事件 2 应有独立发送记录");
        r1!.Id.Should().NotBe(r2!.Id);
    }

    /// <summary>种子 payment_succeeded 模板（幂等：已存在则跳过，避免重复唯一键冲突）。</summary>
    private async Task SeedPaymentTemplateAsync()
    {
        await using var seedScope = ServiceProvider.CreateAsyncScope();
        var db = seedScope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var exists = await db.Set<NotificationTemplate>()
            .AnyAsync(t => t.Code == TemplateCode);
        if (exists) return;
        db.Set<NotificationTemplate>().Add(NotificationTemplate.Create(
            Guid.NewGuid(),
            TemplateCode,
            "支付成功通知",
            NotificationChannel.InApp,
            "支付成功",
            "您的订单已完成支付。",
            variables: []));
        await db.SaveChangesAsync();
    }

    #region 断言辅助

    private async Task<NotificationRecord?> GetRecordAsync(string idempotencyKey)
    {
        await using var scope = ServiceProvider.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<INotificationRecordRepository>();
        return await repo.GetByIdempotencyKeyAsync(idempotencyKey);
    }

    private async Task<int> CountRecordsAsync(string idempotencyKey)
    {
        await using var scope = ServiceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        return await db.Set<NotificationRecord>().CountAsync(r => r.IdempotencyKey == idempotencyKey);
    }

    private static async Task<NotificationRecord?> PollUntilAsync(
        Func<Task<NotificationRecord?>> probe, int timeoutSeconds = 20)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            var record = await probe();
            if (record is not null) return record;
            await Task.Delay(200);
        }

        return await probe();
    }

    #endregion
}
