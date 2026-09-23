using Medallion.Threading;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using StackExchange.Redis;

namespace Leno.Testing.Fixtures;

/// <summary>
/// WebApplicationFactory 测试宿主通用辅助方法。
/// 解决各 BC Api.Tests 的共性问题：
/// 1. Program.cs 启动期敏感配置校验（ValidateSensitiveConfig）在 Testing 环境缺失即抛异常——提供占位键；
/// 2. MigrateWithLockAsync 启动期通过 Redis 分布式锁执行迁移——测试环境无 Redis，替换为返回 null 的 Mock 使迁移跳过。
/// </summary>
public static class TestWebHostHelper
{
    /// <summary>
    /// 敏感配置占位键值对（均为仅测试用的非生产密钥），
    /// 与 Leno.Infrastructure.Persistence.ConfigCenterExtensions.SensitiveConfigKeys 对齐。
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> SensitiveConfigPlaceholders =
        new Dictionary<string, string>
        {
            ["Payment:Alipay:AppId"] = "test-alipay-app-id",
            ["Payment:Alipay:PrivateKey"] = "test-alipay-private-key",
            ["Payment:Alipay:PublicKey"] = "test-alipay-public-key",
            ["Payment:WeChatPay:AppId"] = "test-wechat-app-id",
            ["Payment:WeChatPay:MchId"] = "test-wechat-mch-id",
            ["Payment:WeChatPay:ApiKey"] = "test-wechat-api-key",
            ["SMS:ApiKey"] = "test-sms-api-key",
            ["SMS:ApiSecret"] = "test-sms-api-secret",
            ["OAuth2:WeChat:AppId"] = "test-wechat-oauth-app-id",
            ["OAuth2:WeChat:AppSecret"] = "test-wechat-oauth-app-secret",
            ["OAuth2:Apple:ClientId"] = "test-apple-client-id",
            ["OAuth2:Apple:ClientSecret"] = "test-apple-client-secret",
            // 双轨下线 A6：RS256 时代的唯一密钥类配置 = JWKS 发现文档地址
            ["Jwt:DiscoveryUrl"] = "http://localhost:5162/.well-known/openid-configuration",
        };

    /// <summary>
    /// 为测试宿主注入全部敏感配置占位键，通过 <c>ValidateSensitiveConfig</c> 的启动期校验。
    /// 在 <c>WithWebHostBuilder</c> 回调中调用。
    /// </summary>
    public static void UseSensitiveConfigPlaceholders(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        foreach (var (key, value) in SensitiveConfigPlaceholders)
        {
            builder.UseSetting(key, value);
        }
    }

    /// <summary>
    /// 将 <c>IDistributedLockProvider</c> 替换为 TryAcquireAsync 恒返回 null 的 Mock，
    /// 使 <c>MigrateWithLockAsync&lt;TDbContext&gt;</c> 视为"锁被其他实例持有"而跳过迁移。
    /// 在 <c>ConfigureServices</c> 回调中调用。
    /// </summary>
    public static void ReplaceDistributedLockWithNullProvider(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var lockMock = new Mock<IDistributedLock>();
        lockMock
            .Setup(l => l.TryAcquireAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns(() => default);

        var lockProviderMock = new Mock<IDistributedLockProvider>();
        lockProviderMock
            .Setup(p => p.CreateLock(It.IsAny<string>()))
            .Returns(lockMock.Object);

        var descriptors = services
            .Where(s => s.ServiceType == typeof(IDistributedLockProvider))
            .ToList();
        foreach (var d in descriptors)
        {
            services.Remove(d);
        }

        services.AddSingleton(lockProviderMock.Object);
    }

    /// <summary>
    /// 将 <c>IConnectionMultiplexer</c> 替换为 Mock，
    /// 避免请求链路（幂等去重存储、限流等）触发真实 Redis 连接。
    /// 在 <c>ConfigureServices</c> 回调中调用。
    /// </summary>
    public static void ReplaceRedisWithMock(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var descriptors = services
            .Where(s => s.ServiceType == typeof(IConnectionMultiplexer))
            .ToList();
        foreach (var d in descriptors)
        {
            services.Remove(d);
        }

        services.AddSingleton(new Mock<IConnectionMultiplexer>().Object);
    }

    /// <summary>
    /// 移除 Quartz.NET 调度器相关服务（双轨下线 DEC-2 决策 (b)，2026-09-21）。
    /// <para>
    /// <c>AddEventBus</c> 会注册 Quartz 宿主服务与调度器工厂；宿主服务启动时会校验并连接
    /// 调度库（独立 LenoScheduler），而测试环境没有 SQL Server，会导致宿主启动失败。
    /// 与 <see cref="ReplaceDistributedLockWithNullProvider"/> / <see cref="ReplaceRedisWithMock"/>
    /// 属同一类"测试环境剔除需外部资源的基础设施"的处理。
    /// </para>
    /// <para>
    /// 在 <c>ConfigureServices</c> 回调中调用。
    /// </para>
    /// </summary>
    public static void RemoveQuartzSchedulerServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // 1) 移除 Quartz 宿主服务（启动时做 schema 校验并连接调度库）
        var hosted = services
            .Where(s => s.ImplementationType?.FullName?.EndsWith(
                "QuartzHostedService", StringComparison.Ordinal) == true)
            .ToList();
        foreach (var d in hosted)
        {
            services.Remove(d);
        }

        // 2) 移除调度器工厂，避免请求链路解析 IMessageScheduler 时触发真实数据库连接
        var factories = services
            .Where(s => s.ServiceType.FullName == "Quartz.ISchedulerFactory")
            .ToList();
        foreach (var d in factories)
        {
            services.Remove(d);
        }
    }
}
