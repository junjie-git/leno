using System.Security.Claims;
using System.Text.Encodings.Web;
using Leno.Infrastructure.Abstractions.Sessions;
using Leno.Infrastructure.Auth;
using Leno.SystemAdmin.Api;
using Leno.SystemAdmin.Application;
using Leno.SystemAdmin.Domain.Services;
using Leno.SystemAdmin.Infrastructure;
using Medallion.Threading;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;

namespace Leno.SystemAdmin.Api.Tests;

/// <summary>
/// SystemAdmin API 集成测试工厂（spec §6.9）。
/// 替换 DbContext 为 SQLite in-memory、IDistributedLockProvider 为 Mock（跳过迁移）、
/// ICurrentUserContext 为 Header-based（按 X-Test-* 头注入身份）、应用服务为 Mock 单例。
/// 移除 MassTransit/Elasticsearch/EventBus 与所有 HostedService，避免外部依赖阻塞宿主启动。
/// </summary>
public sealed class SystemAdminApiFactory : WebApplicationFactory<Program>
{
    /// <summary>默认测试用户标识（X-Test-User-Id 未设置时使用）。</summary>
    public static readonly Guid DefaultTestUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    /// <summary>默认测试会话标识（X-Test-Session-Id 未设置时使用）。</summary>
    public const string DefaultTestSessionId = "test-session-id-001";

    // 暴露给测试用例的 Mock 对象（应用服务层）
    public Mock<IMenuAppService> MenuAppServiceMock { get; } = new();
    public Mock<ILoginLogAppService> LoginLogAppServiceMock { get; } = new();
    public Mock<IOnlineUserAppService> OnlineUserAppServiceMock { get; } = new();
    public Mock<ICacheMonitorAppService> CacheMonitorAppServiceMock { get; } = new();
    public Mock<IServerMonitorAppService> ServerMonitorAppServiceMock { get; } = new();
    public Mock<IAuditLogAppService> AuditLogAppServiceMock { get; } = new();
    public Mock<IAuditLogEntryAppService> AuditLogEntryAppServiceMock { get; } = new();
    public Mock<IFeatureFlagAppService> FeatureFlagAppServiceMock { get; } = new();
    public Mock<IAnnouncementAppService> AnnouncementAppServiceMock { get; } = new();

    // 暴露给测试用例的 Mock 对象（基础设施抽象层）
    public Mock<IUserSessionStore> UserSessionStoreMock { get; } = new();
    public Mock<IRedisCacheMonitor> RedisCacheMonitorMock { get; } = new(MockBehavior.Loose);

    private SqliteConnection? _sqliteConnection;

    /// <summary>创建已注入 Admin 角色与默认用户标识的 HttpClient。</summary>
    public HttpClient CreateAdminClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test");
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");
        client.DefaultRequestHeaders.Add("X-Test-User-Id", DefaultTestUserId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Session-Id", DefaultTestSessionId);
        return client;
    }

    /// <summary>创建带指定角色的 HttpClient（用于 RBAC 403 测试）。</summary>
    public HttpClient CreateClientWithRole(string role)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test");
        client.DefaultRequestHeaders.Add("X-Test-Role", role);
        client.DefaultRequestHeaders.Add("X-Test-User-Id", DefaultTestUserId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Session-Id", DefaultTestSessionId);
        return client;
    }

    /// <summary>创建无认证 HttpClient（用于 401 测试）。</summary>
    public HttpClient CreateAnonymousClient()
    {
        return CreateClient();
    }

    /// <summary>初始化数据库（EnsureCreatedAsync 创建表结构）。需在测试用例发送请求前调用一次。</summary>
    public async Task InitializeDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SystemAdminDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // 使用 Development 环境跳过敏感配置校验（Program.cs 仅在非 Development 抛异常）
        builder.UseEnvironment("Development");

        // 提供敏感配置占位值，确保 JwtBearer 与 InternalApiKey 校验通过
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = "test-secret-key-at-least-32-bytes-long-for-hs256-validation",
                ["Jwt:Issuer"] = "Leno.UserAuth",
                ["Jwt:Audience"] = "Leno.Clients",
                ["Security:InternalApiKey:SystemAdmin"] = new string('a', 44),
                ["Security:InternalApiKey:Shared"] = new string('b', 44),
                ["InternalAuth:ApiKey"] = "",
                ["Redis:Configuration"] = "localhost:6379",
                ["RabbitMQ:Host"] = ""
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // 1. 替换 DbContext 为 SQLite in-memory
            services.RemoveAll<DbContextOptions<SystemAdminDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<SystemAdminDbContext>();
            _sqliteConnection = new SqliteConnection("DataSource=:memory:");
            _sqliteConnection.Open();
            services.AddSingleton(_sqliteConnection);
            services.AddDbContext<SystemAdminDbContext>(opt =>
                opt.UseSqlite(_sqliteConnection));

            // 2. 替换 IDistributedLockProvider 为 Mock，使 MigrateWithLockAsync 跳过迁移
            ReplaceDistributedLockProvider(services);

            // 3. 替换 IConnectionMultiplexer 为 Mock，避免 Redis 连接
            ReplaceRedisConnection(services);

            // 4. 移除 MassTransit / Elasticsearch / EventBus
            RemoveMassTransitServices(services);
            RemoveElasticsearchServices(services);
            RemoveEventBusServices(services);

            // 4.1 补充 Mock IEventBus，供 DeadLetterQueueManager 等依赖解析
            services.AddSingleton(_ => new Mock<Leno.Infrastructure.Abstractions.IEventBus>().Object);

            // 5. 移除所有 HostedService（避免后台作业依赖 Redis/Quartz 阻塞启动）
            RemoveHostedServices(services);

            // 6. 替换 ICurrentUserContext 为 Header-based 实现
            services.RemoveAll<ICurrentUserContext>();
            services.AddScoped<ICurrentUserContext, HeaderCurrentUserContext>();

            // 7. 替换应用服务为 Mock 单例
            ReplaceApplicationServices(services);

            // 8. 替换基础设施抽象为 Mock 单例
            services.RemoveAll<IUserSessionStore>();
            services.AddSingleton(UserSessionStoreMock.Object);
            services.RemoveAll<IRedisCacheMonitor>();
            services.AddSingleton(RedisCacheMonitorMock.Object);

            // 9. 添加测试鉴权（覆盖 JwtBearer 默认方案）
            services.AddAuthentication("Test")
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
        });
    }

    private void ReplaceDistributedLockProvider(IServiceCollection services)
    {
        var descriptors = services
            .Where(s => s.ServiceType == typeof(IDistributedLockProvider))
            .ToList();
        foreach (var d in descriptors) services.Remove(d);

        var lockMock = new Mock<IDistributedLock>();
        lockMock
            .Setup(l => l.TryAcquireAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns(() => default);

        var lockProviderMock = new Mock<IDistributedLockProvider>();
        lockProviderMock
            .Setup(p => p.CreateLock(It.IsAny<string>()))
            .Returns(lockMock.Object);

        services.AddSingleton(lockProviderMock.Object);
    }

    private static void ReplaceRedisConnection(IServiceCollection services)
    {
        var descriptors = services
            .Where(s => s.ServiceType == typeof(IConnectionMultiplexer))
            .ToList();
        foreach (var d in descriptors) services.Remove(d);

        var multiplexerMock = new Mock<IConnectionMultiplexer>();
        var databaseMock = new Mock<IDatabase>();
        multiplexerMock.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(databaseMock.Object);
        services.AddSingleton(multiplexerMock.Object);
    }

    private static void RemoveMassTransitServices(IServiceCollection services)
    {
        var descriptors = services
            .Where(s => s.ServiceType.FullName?.Contains("MassTransit") == true
                     || s.ImplementationType?.FullName?.Contains("MassTransit") == true
                     || s.ServiceType == typeof(MassTransit.IBus)
                     || s.ServiceType == typeof(MassTransit.IBusControl)
                     || s.ServiceType.FullName?.StartsWith("MassTransit.", StringComparison.Ordinal) == true)
            .ToList();
        foreach (var d in descriptors) services.Remove(d);
    }

    private static void RemoveElasticsearchServices(IServiceCollection services)
    {
        var descriptors = services
            .Where(s => s.ServiceType.FullName?.Contains("Elasticsearch") == true
                     || s.ServiceType.FullName?.Contains("Elastic") == true
                     || s.ServiceType.FullName?.Contains("Nest") == true
                     || s.ImplementationType?.FullName?.Contains("Elastic") == true)
            .ToList();
        foreach (var d in descriptors) services.Remove(d);
    }

    private static void RemoveEventBusServices(IServiceCollection services)
    {
        var descriptors = services
            .Where(s => s.ServiceType == typeof(Leno.Infrastructure.Abstractions.IEventBus)
                     || s.ImplementationType?.FullName?.Contains("RabbitMqEventBus") == true)
            .ToList();
        foreach (var d in descriptors) services.Remove(d);
    }

    private static void RemoveHostedServices(IServiceCollection services)
    {
        // 仅移除 IHostedService 注册（后台服务、Quartz 托管服务等），
        // 保留 QuartzJobScheduler 与 ISchedulerFactory（StdSchedulerFactory 为内存实现，无外部依赖）
        var descriptors = services
            .Where(s => s.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService))
            .ToList();
        foreach (var d in descriptors) services.Remove(d);
    }

    private void ReplaceApplicationServices(IServiceCollection services)
    {
        ReplaceService(services, MenuAppServiceMock);
        ReplaceService(services, LoginLogAppServiceMock);
        ReplaceService(services, OnlineUserAppServiceMock);
        ReplaceService(services, CacheMonitorAppServiceMock);
        ReplaceService(services, ServerMonitorAppServiceMock);
        ReplaceService(services, AuditLogAppServiceMock);
        ReplaceService(services, AuditLogEntryAppServiceMock);
        ReplaceService(services, FeatureFlagAppServiceMock);
        ReplaceService(services, AnnouncementAppServiceMock);
    }

    private static void ReplaceService<TService>(IServiceCollection services, Mock<TService> mock)
        where TService : class
    {
        var existing = services.Where(s => s.ServiceType == typeof(TService)).ToList();
        foreach (var d in existing) services.Remove(d);
        services.AddSingleton(mock.Object);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _sqliteConnection?.Dispose();
            _sqliteConnection = null;
        }
        base.Dispose(disposing);
    }
}

/// <summary>
/// 测试用 ICurrentUserContext 实现，按请求头 X-Test-User-Id / X-Test-Role / X-Test-Session-Id 注入身份。
/// 头不存在时回退到默认 Admin 身份，便于常规测试用例直接调用。
/// </summary>
public sealed class HeaderCurrentUserContext : ICurrentUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HeaderCurrentUserContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private string? HeaderValue(string name)
        => _httpContextAccessor.HttpContext?.Request.Headers[name].FirstOrDefault();

    public bool IsAuthenticated =>
        _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;

    public Guid? UserId
    {
        get
        {
            var raw = HeaderValue("X-Test-User-Id");
            if (Guid.TryParse(raw, out var id)) return id;
            return SystemAdminApiFactory.DefaultTestUserId;
        }
    }

    public string? Role
    {
        get
        {
            var raw = HeaderValue("X-Test-Role");
            return string.IsNullOrEmpty(raw) ? "Admin" : raw;
        }
    }

    public Guid? ShopId
    {
        get
        {
            var raw = HeaderValue("X-Test-Shop-Id");
            if (Guid.TryParse(raw, out var id)) return id;
            return null;
        }
    }

    public string? SessionId
    {
        get
        {
            var raw = HeaderValue("X-Test-Session-Id");
            return string.IsNullOrEmpty(raw) ? SystemAdminApiFactory.DefaultTestSessionId : raw;
        }
    }
}

/// <summary>
/// 测试鉴权处理器：Authorization 头存在即通过，角色按 X-Test-Role 头注入（逗号分隔）。
/// 头不存在时注入全部角色，[Authorize] 始终通过。
/// </summary>
public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("Authorization"))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "test"),
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
        };

        var testRoleHeader = Request.Headers["X-Test-Role"].FirstOrDefault();
        if (!string.IsNullOrEmpty(testRoleHeader))
        {
            foreach (var role in testRoleHeader.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }
        }
        else
        {
            claims.Add(new Claim(ClaimTypes.Role, "Buyer"));
            claims.Add(new Claim(ClaimTypes.Role, "Seller"));
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));
            claims.Add(new Claim(ClaimTypes.Role, "Operator"));
        }

        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
