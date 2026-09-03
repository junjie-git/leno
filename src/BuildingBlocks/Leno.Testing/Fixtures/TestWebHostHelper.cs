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
}
