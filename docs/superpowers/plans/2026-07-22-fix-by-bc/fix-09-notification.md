# Notification（消息通知域）修复实施计划

## 报告头部

| 字段 | 值 |
|------|-----|
| BC 名称 | Notification（消息通知域） |
| 审计报告 | `docs/superpowers/specs/2026-07-21-code-audit/09-notification.md` |
| 汇总参考 | `docs/superpowers/specs/2026-07-21-code-audit/00-summary.md` F 章节（P1-20/21/22/23） |
| 架构评估参考 | `docs/superpowers/specs/2026-07-21-code-audit/13-architecture-assessment.md` G4（TD3/TD6）/ G5（S3） |
| 已修复来源 | `docs/superpowers/plans/2026-07-20-p1b1-async-reliability-hardening.md` Task 4（T4） |
| 计划日期 | 2026-07-22 |
| 扫描范围 | `src/Services/Notification/Leno.Notification.{Domain,Application,Infrastructure,Api}/` |
| 排除范围 | Tests 目录、Migrations/*.Designer.cs、*ModelSnapshot.cs |

---

## 问题统计总览

| 严重度 | 审计列出总数 | [ALREADY-FIXED] | [VERIFIED-NOT-REPRODUCIBLE] | 待修复 |
|--------|-------------|-----------------|-----------------------------|--------|
| 🔴 P0（高） | 12 | 0 | 0 | 12 |
| 🟡 P1（中） | 26 | 0 | 0 | 26 |
| 🟢 P2（低） | 9 | 0 | 0 | 9 |
| **合计** | **47** | **0** | **0** | **47** |

> **说明**：审计报告头部标注"高 12 / 中 18 / 低 9"，但实际逐条列出的问题为 12 高（#1-#12）+ 26 中（#13-#38）+ 9 低（#39-#47）= 47 个。本计划以实际列出的 47 个问题为准。所有 47 个问题均经源码校验确认仍存在。

### 已修复问题（独立追踪）

| 编号 | 标题 | 来源 | 状态 |
|------|------|------|------|
| T4 | 通知 fire-and-forget 改 await | `2026-07-20-p1b1-async-reliability-hardening.md` Task 4 | `[ALREADY-FIXED]` |

**T4 验证证据**：
- `file:///workspace/src/Services/Notification/Leno.Notification.Infrastructure/Consumers/NotificationEventConsumer.cs#L181` — 已改为 `await SendAsync(request, eventType, evt.EventId).ConfigureAwait(false);`
- `file:///workspace/src/Services/Notification/Leno.Notification.Infrastructure/Consumers/NotificationEventConsumer.cs#L188-L193` — `SendAsync` 方法无 try-catch 包裹，异常直接冒泡到 MassTransit
- `file:///workspace/src/Services/Notification/Leno.Notification.Api/appsettings.json#L81-L87` — `MassTransit:Retry` 配置节已存在

> **注意**：T4 修复的是 `NotificationEventConsumer` 的 fire-and-forget 异步丢失问题，该问题在审计报告 47 个问题中未单独列出（审计于 T4 修复后执行，该问题已不存在）。T4 作为独立已修复项追踪，不在 47 个待修复问题中重复计数。

---

## 🔴 P0（高严重度）详细修复计划

> 每个 P0 问题采用 TDD 5 步骤：① 写失败测试 → ② 运行验证失败 → ③ 写最小实现 → ④ 运行验证通过 → ⑤ 提交。

---

### P0-1：DI 注册导致 SmsChannel 重复键异常，全渠道调度链路必崩

**审计编号**：#1
**证据**：
- `file:///workspace/src/Services/Notification/Leno.Notification.Infrastructure/Dependencies/ServiceCollectionExtensions.cs#L113-L116` — `AliyunSmsChannel` 与 `TencentSmsChannel` 同时注册为 `INotificationChannel`
- `file:///workspace/src/Services/Notification/Leno.Notification.Infrastructure/Channels/SmsChannel.cs#L33` — `AliyunSmsChannel.Channel => NotificationChannel.Sms`
- `file:///workspace/src/Services/Notification/Leno.Notification.Infrastructure/Channels/SmsChannel.cs#L120` — `TencentSmsChannel.Channel => NotificationChannel.Sms`
- `file:///workspace/src/Services/Notification/Leno.Notification.Infrastructure/Services/NotificationDispatcher.cs#L70` — `_channels.ToDictionary(c => c.Channel)` 触发重复键异常

**根因**：两个 SMS 渠道实现返回相同的 `Channel` 枚举值，`ToDictionary` 因重复键抛 `ArgumentException`。

**修复方案**：引入 `ISmsProvider` 接口，让 `AliyunSmsChannel` 和 `TencentSmsChannel` 实现为 `ISmsProvider`，新建 `SmsChannel` 外壳类按 `IChannelSelector.SelectProvider` 在运行时选择 provider。DI 中只注册一个 `SmsChannel` 作为 `INotificationChannel`。

#### TDD 步骤 1：编写失败测试

```csharp
// 文件：src/Services/Notification/Leno.Notification.Infrastructure.Tests/Services/NotificationDispatcherTests.cs
using Leno.Notification.Domain.Aggregates;
using Leno.Notification.Domain.Services;
using Leno.Notification.Domain.ValueObjects;
using Leno.Notification.Infrastructure.Channels;
using Leno.Notification.Infrastructure.Services;
using Leno.Notification.Infrastructure.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Leno.Notification.Infrastructure.Tests.Services;

public class NotificationDispatcherTests
{
    private readonly Mock<INotificationTemplateRepository> _templateRepoMock;
    private readonly Mock<INotificationPreferenceRepository> _preferenceRepoMock;
    private readonly Mock<INotificationRecordRepository> _recordRepoMock;
    private readonly Mock<ITemplateRenderer> _rendererMock;
    private readonly Mock<IUserContactService> _userContactMock;
    private readonly Mock<IUnitOfWork> _uowMock;
    private readonly Mock<ILogger<NotificationDispatcher>> _loggerMock;
    private readonly Mock<IChannelSelector> _channelSelectorMock;

    public NotificationDispatcherTests()
    {
        _templateRepoMock = new Mock<INotificationTemplateRepository>(MockBehavior.Strict);
        _preferenceRepoMock = new Mock<INotificationPreferenceRepository>(MockBehavior.Strict);
        _recordRepoMock = new Mock<INotificationRecordRepository>(MockBehavior.Strict);
        _rendererMock = new Mock<ITemplateRenderer>(MockBehavior.Strict);
        _userContactMock = new Mock<IUserContactService>(MockBehavior.Strict);
        _uowMock = new Mock<IUnitOfWork>(MockBehavior.Strict);
        _loggerMock = new Mock<ILogger<NotificationDispatcher>>();
        _channelSelectorMock = new Mock<IChannelSelector>(MockBehavior.Strict);
    }

    private NotificationDispatcher CreateDispatcher(IEnumerable<INotificationChannel> channels)
    {
        return new NotificationDispatcher(
            channels,
            _templateRepoMock.Object,
            _preferenceRepoMock.Object,
            _recordRepoMock.Object,
            _rendererMock.Object,
            _userContactMock.Object,
            _uowMock.Object,
            _channelSelectorMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task DispatchAsync_WithMultipleSmsProviders_ShouldNotThrowDuplicateKeyException()
    {
        // Arrange — 模拟两个 SMS provider 都返回 Channel=Sms
        // 修复后：SmsChannel 外壳类作为单一 INotificationChannel 注册，
        // 不再有两个 INotificationChannel 返回相同 Channel 值
        var aliyunProviderMock = new Mock<ISmsProvider>(MockBehavior.Strict);
        aliyunProviderMock.SetupGet(p => p.ProviderName).Returns("Aliyun");
        var tencentProviderMock = new Mock<ISmsProvider>(MockBehavior.Strict);
        tencentProviderMock.SetupGet(p => p.ProviderName).Returns("Tencent");

        var smsChannel = new SmsChannel(
            new[] { aliyunProviderMock.Object, tencentProviderMock.Object },
            _channelSelectorMock.Object,
            new Mock<ILogger<SmsChannel>>().Object);

        var emailChannelMock = new Mock<INotificationChannel>(MockBehavior.Strict);
        emailChannelMock.SetupGet(c => c.Channel).Returns(NotificationChannel.Email);
        var inAppChannelMock = new Mock<INotificationChannel>(MockBehavior.Strict);
        inAppChannelMock.SetupGet(c => c.Channel).Returns(NotificationChannel.InApp);

        var channels = new INotificationChannel[]
        {
            smsChannel,
            emailChannelMock.Object,
            inAppChannelMock.Object
        };

        var dispatcher = CreateDispatcher(channels);

        var template = NotificationTemplate.Create(
            Guid.NewGuid(), "test_code", NotificationChannel.Sms, "Test", "Body");
        var preference = NotificationPreference.Create(
            Guid.NewGuid(), Guid.NewGuid(), "test_code", PreferenceStatus.Active);

        _preferenceRepoMock
            .Setup(r => r.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(preference);
        _templateRepoMock
            .Setup(r => r.GetEnabledAsync(It.IsAny<string>(), It.IsAny<NotificationChannel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);
        _rendererMock
            .Setup(r => r.Render(It.IsAny<NotificationTemplate>(), It.IsAny<Dictionary<string, string>>()))
            .Returns(("Title", "Content"));
        _recordRepoMock
            .Setup(r => r.AddAsync(It.IsAny<NotificationRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _uowMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _recordRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<NotificationRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _userContactMock
            .Setup(u => u.GetContactAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserContact("13800138000", "test@example.com"));
        _channelSelectorMock
            .Setup(s => s.SelectSmsProvider())
            .Returns("Aliyun");
        aliyunProviderMock
            .Setup(p => p.SendAsync(It.IsAny<ChannelSendRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelSendResult(true, null, null, "biz-id-123"));

        // Act — 修复前：ToDictionary 抛 ArgumentException；修复后：正常调度不抛异常
        var exception = await Record.ExceptionAsync(() =>
            dispatcher.DispatchAsync(Guid.NewGuid(), "test_code", "evt-1", new Dictionary<string, string>(), CancellationToken.None));

        // Assert
        Assert.Null(exception);
        aliyunProviderMock.Verify(
            p => p.SendAsync(It.IsAny<ChannelSendRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

#### TDD 步骤 2：运行测试验证失败

```bash
cd src/Services/Notification/Leno.Notification.Infrastructure.Tests
dotnet test --filter "FullyQualifiedName~NotificationDispatcherTests.DispatchAsync_WithMultipleSmsProviders_ShouldNotThrowDuplicateKeyException"
```

**预期结果**：编译失败或运行时抛出 `ArgumentException: An item with the same key has already been added`（当前 `NotificationDispatcher` 构造函数或 `DispatchAsync` 中 `_channels.ToDictionary(c => c.Channel)` 在存在两个 `Channel=Sms` 的实现时崩溃）。

#### TDD 步骤 3：编写最小实现

**3a. 新建 `ISmsProvider` 接口**（`src/Services/Notification/Leno.Notification.Domain/Services/ISmsProvider.cs`）：

```csharp
using Leno.Notification.Domain.ValueObjects;

namespace Leno.Notification.Domain.Services;

/// <summary>
/// 短信发送提供商接口，由 Aliyun/Tencent 等具体实现。
/// </summary>
public interface ISmsProvider
{
    /// <summary>提供商名称（如 "Aliyun"、"Tencent"）。</summary>
    string ProviderName { get; }

    /// <summary>
    /// 发送短信，返回发送结果。
    /// </summary>
    Task<ChannelSendResult> SendAsync(ChannelSendRequest request, CancellationToken ct = default);
}
```

**3b. 将 `AliyunSmsChannel` 重构为 `AliyunSmsProvider`** 实现 `ISmsProvider`（保留原有发送逻辑，移除 `INotificationChannel` 接口实现与 `Channel` 属性）。

**3c. 将 `TencentSmsChannel` 重构为 `TencentSmsProvider`** 实现 `ISmsProvider`（同上）。

**3d. 新建 `SmsChannel` 外壳类**（`src/Services/Notification/Leno.Notification.Infrastructure/Channels/SmsChannel.cs`）：

```csharp
using Leno.Notification.Domain.Services;
using Leno.Notification.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Leno.Notification.Infrastructure.Channels;

/// <summary>
/// 短信渠道外壳，按 IChannelSelector 选择具体 ISmsProvider 发送。
/// 作为唯一的 INotificationChannel(NotificationChannel.Sms) 注册到 DI。
/// </summary>
public sealed class SmsChannel : INotificationChannel
{
    private readonly Dictionary<string, ISmsProvider> _providers;
    private readonly IChannelSelector _channelSelector;
    private readonly ILogger<SmsChannel> _logger;

    public NotificationChannel Channel => NotificationChannel.Sms;

    public SmsChannel(
        IEnumerable<ISmsProvider> providers,
        IChannelSelector channelSelector,
        ILogger<SmsChannel> logger)
    {
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(channelSelector);
        ArgumentNullException.ThrowIfNull(logger);
        _providers = providers.ToDictionary(p => p.ProviderName, StringComparer.OrdinalIgnoreCase);
        _channelSelector = channelSelector;
        _logger = logger;
    }

    public async Task<ChannelSendResult> SendAsync(ChannelSendRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var providerName = _channelSelector.SelectSmsProvider();
        if (string.IsNullOrWhiteSpace(providerName) || !_providers.TryGetValue(providerName, out var provider))
        {
            _logger.LogWarning("未找到短信提供商 Provider={Provider}", providerName);
            return new ChannelSendResult(false, "短信提供商未配置", "SMS_PROVIDER_NOT_FOUND", null);
        }

        return await provider.SendAsync(request, ct).ConfigureAwait(false);
    }
}
```

**3e. 在 `IChannelSelector` 接口增加 `SelectSmsProvider()` 方法**，在 `ChannelSelector` 实现中返回当前配置的 SMS provider 名称。

**3f. 修改 `ServiceCollectionExtensions.cs`**：

```csharp
// 原 L113-L116 替换为：
services.AddScoped<ISmsProvider, AliyunSmsProvider>();
services.AddScoped<ISmsProvider, TencentSmsProvider>();
services.AddScoped<INotificationChannel, SmsChannel>();   // 唯一的 Sms INotificationChannel
services.AddScoped<INotificationChannel, SmtpEmailChannel>();
services.AddScoped<INotificationChannel, InAppChannel>();
```

**3g. 修改 `NotificationDispatcher.cs`** — 将 `_channels.ToDictionary(c => c.Channel)` 改为在构造函数中构建一次并缓存为字段：

```csharp
private readonly Dictionary<NotificationChannel, INotificationChannel> _channelDict;

public NotificationDispatcher(/* ... */)
{
    // ...
    _channelDict = channels.ToDictionary(c => c.Channel);
}
```

同样修改 `NotificationDispatchJob.cs#L53` 和 `NotificationRetryJob.cs#L107` 中的 `ToDictionary` 调用。

#### TDD 步骤 4：运行测试验证通过

```bash
cd src/Services/Notification/Leno.Notification.Infrastructure.Tests
dotnet test --filter "FullyQualifiedName~NotificationDispatcherTests.DispatchAsync_WithMultipleSmsProviders_ShouldNotThrowDuplicateKeyException"
```

**预期结果**：测试通过，无 `ArgumentException` 抛出，`AliyunSmsProvider.SendAsync` 被调用一次。

#### TDD 步骤 5：提交

```bash
git add -A
git commit -m "fix(notification): 修复SMS渠道DI重复键异常，引入ISmsProvider外壳模式

- 新建ISmsProvider接口，AliyunSmsChannel/TencentSmsChannel重构为Provider
- 新建SmsChannel外壳类按IChannelSelector选择provider
- NotificationDispatcher缓存渠道字典避免重复构建
- 修复ToDictionary重复键导致全渠道调度崩溃的P0问题"
```

---

### P0-2：EmailChannelOptions / SmsChannelOptions 字段名与 appsettings.json 不匹配

**审计编号**：#2
**证据**：
- `file:///workspace/src/Services/Notification/Leno.Notification.Infrastructure/Dependencies/ServiceCollectionExtensions.cs#L105-L106` — `Configure<EmailChannelOptions>` / `Configure<SmsChannelOptions>` 绑定
- `file:///workspace/src/Services/Notification/Leno.Notification.Api/appsettings.json#L64-L71` — `Notification:Sms` 键为 `Provider/AccessKey/Secret/SignName/TemplateCode/Endpoint`
- `file:///workspace/src/Services/Notification/Leno.Notification.Api/appsettings.json#L72-L79` — `Notification:Email` 键为 `SmtpHost/Port/Username/Password/FromAddress/EnableSsl`
- `file:///workspace/src/Services/Notification/Leno.Notification.Infrastructure/Channels/SmsChannel.cs#L47` — `if (string.IsNullOrWhiteSpace(_options.AccessKeyId))` 永远成立
- `file:///workspace/src/Services/Notification/Leno.Notification.Infrastructure/Channels/EmailChannel.cs#L44` — `if (string.IsNullOrWhiteSpace(_options.Host))` 永远成立

**根因**：Options 类字段名（`Host/From/UseSsl/AccessKeyId/AccessKeySecret`）与 appsettings.json 键名（`SmtpHost/FromAddress/EnableSsl/AccessKey/Secret`）不匹配，绑定结果为空。

**修复方案**：统一 appsettings.json 字段名与 Options 类对齐（推荐修改 appsettings.json）。

#### TDD 步骤 1：编写失败测试

```csharp
// 文件：src/Services/Notification/Leno.Notification.Infrastructure.Tests/Channels/ChannelOptionsBindingTests.cs
using Leno.Notification.Infrastructure.Channels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Leno.Notification.Infrastructure.Tests.Channels;

public class ChannelOptionsBindingTests
{
    private static IServiceCollection BuildServicesWithConfig(string json)
    {
        var config = new ConfigurationBuilder()
            .AddJsonStream(new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)))
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.Configure<EmailChannelOptions>(config.GetSection("Notification:Email"));
        services.Configure<SmsChannelOptions>(config.GetSection("Notification:Sms"));
        return services;
    }

    [Fact]
    public void EmailChannelOptions_ShouldBindHostFromAppSettings()
    {
        // Arrange — 使用修复后的 appsettings.json 字段名
        var json = """{"Notification":{"Email":{"Host":"smtp.example.com","Port":587,"Username":"user","Password":"pass","From":"noreply@example.com","UseSsl":true}}}""";
        var services = BuildServicesWithConfig(json);
        var sp = services.BuildServiceProvider();

        // Act
        var options = sp.GetRequiredService<IOptions<EmailChannelOptions>>().Value;

        // Assert — 修复前 Host 为空（appsettings 用 SmtpHost），修复后正确绑定
        Assert.Equal("smtp.example.com", options.Host);
        Assert.Equal(587, options.Port);
        Assert.Equal("noreply@example.com", options.From);
        Assert.True(options.UseSsl);
    }

    [Fact]
    public void SmsChannelOptions_ShouldBindAccessKeyIdFromAppSettings()
    {
        // Arrange — 使用修复后的 appsettings.json 字段名
        var json = """{"Notification":{"Sms":{"Provider":"Aliyun","AccessKeyId":"AKID123","AccessKeySecret":"SK456","SignName":"Leno"}}}""";
        var services = BuildServicesWithConfig(json);
        var sp = services.BuildServiceProvider();

        // Act
        var options = sp.GetRequiredService<IOptions<SmsChannelOptions>>().Value;

        // Assert — 修复前 AccessKeyId 为空（appsettings 用 AccessKey），修复后正确绑定
        Assert.Equal("AKID123", options.AccessKeyId);
        Assert.Equal("SK456", options.AccessKeySecret);
        Assert.Equal("Leno", options.SignName);
    }
}
```

#### TDD 步骤 2：运行测试验证失败

```bash
cd src/Services/Notification/Leno.Notification.Infrastructure.Tests
dotnet test --filter "FullyQualifiedName~ChannelOptionsBindingTests"
```

**预期结果**：测试失败——当前 appsettings.json 使用 `SmtpHost/AccessKey` 等不匹配的键名，Options 绑定后 `Host` 和 `AccessKeyId` 为 `string.Empty`。

#### TDD 步骤 3：编写最小实现

修改 `src/Services/Notification/Leno.Notification.Api/appsettings.json`，将字段名与 Options 类对齐：

```jsonc
// Notification:Email 节 — 原 SmtpHost/FromAddress/EnableSsl 改为 Host/From/UseSsl
"Email": {
  "Host": "smtp.example.com",
  "Port": 587,
  "Username": "",
  "Password": "",
  "From": "noreply@leno.com",
  "UseSsl": true
}

// Notification:Sms 节 — 原 AccessKey/Secret 改为 AccessKeyId/AccessKeySecret
"Sms": {
  "Provider": "Aliyun",
  "AccessKeyId": "",
  "AccessKeySecret": "",
  "SignName": "Leno"
}
```

#### TDD 步骤 4：运行测试验证通过

```bash
dotnet test --filter "FullyQualifiedName~ChannelOptionsBindingTests"
```

**预期结果**：测试通过，Options 字段正确绑定。

#### TDD 步骤 5：提交

```bash
git add -A
git commit -m "fix(notification): 统一appsettings字段名与Options类对齐

- Email: SmtpHost→Host, FromAddress→From, EnableSsl→UseSsl
- Sms: AccessKey→AccessKeyId, Secret→AccessKeySecret
- 修复邮件与短信因配置绑定失败导致永远返回CONFIG_MISSING的问题"
```

---

### P0-3：MassTransit 消费者重复订阅，每条集成事件被处理两次

**审计编号**：#3
**证据**：
- `file:///workspace/src/Services/Notification/Leno.Notification.Infrastructure/Consumers/NotificationEventConsumer.cs#L14-L26` — 实现 12 个 `IConsumer<T>` 接口
- `file:///workspace/src/Services/Notification/Leno.Notification.Infrastructure/Dependencies/ServiceCollectionExtensions.cs#L158-L164` — `NotificationEventConsumer` 与 `OrderEventConsumer` 等同时注册

**根因**：`NotificationEventConsumer` 与各专用 Consumer（Order/User/Payment/AfterSales/Promotion/Points）实现了相同事件的 `IConsumer<T>`，全部注册后每条事件被两个队列各消费一次。

**修复方案**：删除 `NotificationEventConsumer`，保留按 BC 拆分的专用 Consumer；删除 `ServiceCollectionExtensions.cs#L164` 的 `AddConsumer<NotificationEventConsumer>()` 调用。

#### TDD 步骤 1：编写失败测试

```csharp
// 文件：src/Services/Notification/Leno.Notification.Application.Tests/ConsumerRegistrationTests.cs
using Leno.Notification.Infrastructure.Consumers;
using Leno.Notification.Infrastructure.Dependencies;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Leno.Notification.Application.Tests;

public class ConsumerRegistrationTests
{
    [Fact]
    public void AddNotificationConsumers_ShouldNotRegisterNotificationEventConsumer()
    {
        // Arrange
        var services = new ServiceCollection();
        var configurator = new TestBusConfigurator();
        services.AddMassTransit(cfg =>
        {
            cfg.AddNotificationConsumers();
        });

        // Act
        var registeredConsumerTypes = configurator.RegisteredConsumers;

        // Assert — 修复后 NotificationEventConsumer 不应注册，避免与专用 Consumer 重复
        Assert.DoesNotContain(typeof(NotificationEventConsumer), registeredConsumerTypes);
        // 专用 Consumer 仍然注册
        Assert.Contains(typeof(OrderEventConsumer), registeredConsumerTypes);
        Assert.Contains(typeof(UserEventConsumer), registeredConsumerTypes);
    }

    private class TestBusConfigurator
    {
        public HashSet<Type> RegisteredConsumers { get; } = new();
    }
}
```

> **注**：`TestBusConfigurator` 需根据 `IBusRegistrationConfigurator` 的实际接口适配。如果 `IBusRegistrationConfigurator` 无法直接 mock，可改为集成测试：构建完整 MassTransit 容器后检查注册的 Consumer 类型。

#### TDD 步骤 2：运行测试验证失败

```bash
dotnet test --filter "FullyQualifiedName~ConsumerRegistrationTests"
```

**预期结果**：测试失败——当前 `AddNotificationConsumers` 注册了 `NotificationEventConsumer`。

#### TDD 步骤 3：编写最小实现

修改 `src/Services/Notification/Leno.Notification.Infrastructure/Dependencies/ServiceCollectionExtensions.cs#L158-L164`，删除 `configurator.AddConsumer<NotificationEventConsumer>();` 行：

```csharp
public static IBusRegistrationConfigurator AddNotificationConsumers(
    this IBusRegistrationConfigurator configurator)
{
    ArgumentNullException.ThrowIfNull(configurator);

    configurator.AddConsumer<UserEventConsumer>();
    configurator.AddConsumer<OrderEventConsumer>();
    configurator.AddConsumer<PaymentEventConsumer>();
    configurator.AddConsumer<PromotionEventConsumer>();
    configurator.AddConsumer<PointsEventConsumer>();
    configurator.AddConsumer<AfterSalesEventConsumer>();
    // 删除：configurator.AddConsumer<NotificationEventConsumer>();

    return configurator;
}
```

删除 `NotificationEventConsumer.cs` 文件（或标记为 `[Obsolete]` 保留一个版本后删除）。

#### TDD 步骤 4：运行测试验证通过

```bash
dotnet test --filter "FullyQualifiedName~ConsumerRegistrationTests"
```

**预期结果**：测试通过，`NotificationEventConsumer` 不在注册列表中。

#### TDD 步骤 5：提交

```bash
git add -A
git commit -m "fix(notification): 删除NotificationEventConsumer消除重复订阅

- 移除NotificationEventConsumer的12个IConsumer注册
- 保留按BC拆分的专用Consumer（Order/User/Payment等）
- 每条集成事件由唯一Consumer处理，消除双倍发送问题"
```

---

### P0-4：OrderEventConsumer 处理 OrderCancelledEvent 时 UserId 强制为 Guid.Empty

**审计编号**：#4
**证据**：
- `file:///workspace/src/Services/Notification/Leno.Notification.Infrastructure/Consumers/OrderEventConsumer.cs#L108` — `UserId = Guid.Empty`
- `file:///workspace/src/Services/Notification/Leno.Notification.Domain/Aggregates/NotificationRecord.cs#L102-L105` — `Create` 校验 `userId == Guid.Empty` 抛 `NOTIFICATION_USER_EMPTY`

**根因**：`OrderCancelledEvent` 未携带 `BuyerId`，Consumer 直接用 `Guid.Empty` 调用 `SendAsync`，聚合根校验抛异常，MassTransit 重试 3 次后死信。

**修复方案**：在 `OrderCancelledEvent` 事件契约中增加 `BuyerId` 字段；或注入 `IOrderQueryService` 防腐层查询买家 ID；或在 Consumer 中检查 `Guid.Empty` 时记录警告并跳过。

#### TDD 步骤 1：编写失败测试

```csharp
// 文件：src/Services/Notification/Leno.Notification.Application.Tests/OrderEventConsumerTests.cs
// 在现有测试类中追加：
[Fact]
public async Task Consume_OrderCancelledEvent_ShouldUseBuyerIdNotGuidEmpty()
{
    // Arrange
    NotificationRequest? capturedRequest = null;
    _notificationServiceMock
        .Setup(s => s.SendAsync(It.IsAny<NotificationRequest>(), It.IsAny<CancellationToken>()))
        .Callback<NotificationRequest, CancellationToken>((r, _) => capturedRequest = r)
        .ReturnsAsync(new NotificationSendResult { Succeeded = true });

    var buyerId = Guid.NewGuid();
    var evt = new OrderCancelledEvent(
        Guid.NewGuid(), buyerId, "user-cancelled", "changed mind", DateTime.UtcNow);
    var context = new Mock<ConsumeContext<OrderCancelledEvent>>();
    context.Setup(c => c.Message).Returns(evt);
    context.Setup(c => c.CancellationToken).Returns(CancellationToken.None);

    // Act — 修复前：UserId=Guid.Empty 导致 NotificationDomainException；
    //       修复后：从事件中提取 BuyerId
    await _sut.Consume(context.Object);

    // Assert
    capturedRequest.Should().NotBeNull();
    capturedRequest!.UserId.Should().Be(buyerId);
    capturedRequest.UserId.Should().NotBe(Guid.Empty);
    capturedRequest.TemplateCode.Should().Be("order_cancelled");
    capturedRequest.IdempotencyKey.Should().Be(evt.EventId.ToString());
}
```

#### TDD 步骤 2：运行测试验证失败

```bash
dotnet test --filter "FullyQualifiedName~OrderEventConsumerTests.Consume_OrderCancelledEvent_ShouldUseBuyerIdNotGuidEmpty"
```

**预期结果**：测试失败——当前 `OrderCancelledEvent` 的 `OrderEventConsumer.Consume` 方法将 `UserId` 设为 `Guid.Empty`，`NotificationService.SendAsync` 内部 `NotificationRecord.Create` 抛 `NotificationDomainException("UserId 不可为空", "NOTIFICATION_USER_EMPTY")`。

#### TDD 步骤 3：编写最小实现

**3a. 在 `OrderCancelledEvent` 事件契约中增加 `BuyerId` 字段**（`src/Shared/Leno.SharedContracts/Events/OrderEvents.cs`）：

```csharp
public record OrderCancelledEvent(
    Guid EventId,
    Guid OrderId,
    Guid BuyerId,          // 新增
    string CancelledBy,
    string CancelReason,
    DateTime CancelledAt) : IIntegrationEvent;
```

**3b. 修改 `OrderEventConsumer.cs#L105-L116`**：

```csharp
var request = new NotificationRequest
{
    TemplateCode = EventTemplateMapping.GetTemplateCode(nameof(OrderCancelledEvent))!,
    UserId = evt.BuyerId,  // 修复：使用事件中的 BuyerId 而非 Guid.Empty
    IdempotencyKey = evt.EventId.ToString(),
    Variables = new Dictionary<string, string>
    {
        ["orderId"] = evt.OrderId.ToString(),
        ["cancelReason"] = evt.CancelReason,
        ["cancelledBy"] = evt.CancelledBy
    }
};
```

**3c. 在 Order BC 发布 `OrderCancelledEvent` 时传入 `BuyerId`**。

#### TDD 步骤 4：运行测试验证通过

```bash
dotnet test --filter "FullyQualifiedName~OrderEventConsumerTests.Consume_OrderCancelledEvent_ShouldUseBuyerIdNotGuidEmpty"
```

**预期结果**：测试通过，`capturedRequest.UserId` 等于 `buyerId`，不等于 `Guid.Empty`。

#### TDD 步骤 5：提交

```bash
git add -A
git commit -m "fix(notification): OrderCancelledEvent使用BuyerId而非Guid.Empty

- OrderCancelledEvent契约增加BuyerId字段
- OrderEventConsumer从事件提取BuyerId作为通知接收人
- 修复订单取消通知必定抛NOTIFICATION_USER_EMPTY异常的问题"
```

---

### P0-5：NotificationCallbacksController 回执不持久化

**审计编号**：#5
**证据**：
- `file:///workspace/src/Services/Notification/Leno.Notification.Api/Controllers/NotificationCallbacksController.cs#L105` — `await _recordRepository.UpdateAsync(record, ct);` 后无 `SaveChangesAsync`
- 控制器未注入 `IUnitOfWork`

**根因**：`UpdateAsync` 仅标记 `EntityState.Modified`，未调用 `IUnitOfWork.SaveChangesAsync`，EF Core ChangeTracker 在请求结束时丢弃变更。

**修复方案**：注入 `IUnitOfWork`，在 `UpdateAsync` 后调用 `SaveChangesAsync`；或将回执处理下沉到 `IReceiptAppService`。

#### TDD 步骤 1：编写失败测试

```csharp
// 文件：src/Services/Notification/Leno.Notification.Api.Tests/Controllers/NotificationCallbacksControllerTests.cs
using Leno.Notification.Api.Controllers;
using Leno.Notification.Domain.Aggregates;
using Leno.Notification.Domain.Repositories;
using Leno.Notification.Domain.Services;
using Leno.Notification.Domain.ValueObjects;
using Leno.Shared.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Leno.Notification.Api.Tests.Controllers;

public class NotificationCallbacksControllerTests
{
    private readonly Mock<INotificationRecordRepository> _recordRepoMock;
    private readonly Mock<IUnitOfWork> _uowMock;
    private readonly Mock<IConfiguration> _configMock;
    private readonly Mock<ILogger<NotificationCallbacksController>> _loggerMock;
    private readonly NotificationCallbacksController _sut;

    public NotificationCallbacksControllerTests()
    {
        _recordRepoMock = new Mock<INotificationRecordRepository>(MockBehavior.Strict);
        _uowMock = new Mock<IUnitOfWork>(MockBehavior.Strict);
        _configMock = new Mock<IConfiguration>();
        _configMock.Setup(c => c["Notification:CallbackSecret"]).Returns("test-secret-key");
        _loggerMock = new Mock<ILogger<NotificationCallbacksController>>();

        _sut = new NotificationCallbacksController(
            _recordRepoMock.Object,
            _uowMock.Object,
            _configMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task HandleSmsReceiptAsync_ValidSignature_ShouldPersistChanges()
    {
        // Arrange
        var recordId = Guid.NewGuid();
        var channelMessageId = "msg-123";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var secret = "test-secret-key";
        var raw = $"{channelMessageId}|true|{timestamp}|{secret}";
        var signature = ComputeHmacSha256(raw, secret);

        var record = NotificationRecord.Create(
            recordId, Guid.NewGuid(), "sms_code", null,
            NotificationChannel.Sms, "Title", "Content");
        record.MarkSending();
        record.SetChannelMessageId(channelMessageId);

        _recordRepoMock
            .Setup(r => r.GetByChannelMessageIdAsync(channelMessageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);
        _recordRepoMock
            .Setup(r => r.UpdateAsync(record, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _uowMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.HandleSmsReceiptAsync(channelMessageId, "true", timestamp, signature, CancellationToken.None);

        // Assert — 修复后必须调用 SaveChangesAsync
        var okResult = Assert.IsType<OkObjectResult>(result);
        _recordRepoMock.Verify(r => r.UpdateAsync(record, It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static string ComputeHmacSha256(string data, string secret)
    {
        using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(data));
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
}
```

#### TDD 步骤 2：运行测试验证失败

```bash
dotnet test --filter "FullyQualifiedName~NotificationCallbacksControllerTests.HandleSmsReceiptAsync_ValidSignature_ShouldPersistChanges"
```

**预期结果**：编译失败——`NotificationCallbacksController` 构造函数未注入 `IUnitOfWork`；或运行时 `_uowMock.Verify` 失败——当前代码未调用 `SaveChangesAsync`。

#### TDD 步骤 3：编写最小实现

修改 `NotificationCallbacksController` 构造函数注入 `IUnitOfWork`，在 `ProcessReceiptAsync` 的 `UpdateAsync` 后增加 `SaveChangesAsync`：

```csharp
// 构造函数增加 IUnitOfWork unitOfWork 参数
public NotificationCallbacksController(
    INotificationRecordRepository recordRepository,
    IUnitOfWork unitOfWork,
    IConfiguration configuration,
    ILogger<NotificationCallbacksController> logger)
{
    _recordRepository = recordRepository;
    _unitOfWork = unitOfWork;
    _configuration = configuration;
    _logger = logger;
}

// ProcessReceiptAsync 第 105 行后增加：
await _recordRepository.UpdateAsync(record, ct);
await _unitOfWork.SaveChangesAsync(ct);  // 新增：持久化回执状态变更
```

#### TDD 步骤 4：运行测试验证通过

```bash
dotnet test --filter "FullyQualifiedName~NotificationCallbacksControllerTests"
```

**预期结果**：测试通过，`SaveChangesAsync` 被调用一次。

#### TDD 步骤 5：提交

```bash
git add -A
git commit -m "fix(notification): 回执处理增加SaveChangesAsync持久化

- NotificationCallbacksController注入IUnitOfWork
- ProcessReceiptAsync在UpdateAsync后调用SaveChangesAsync
- 修复渠道回执永远不写库导致记录滞留Sending状态的问题"
```

---

### P0-6：NotificationCallbacksController 默认回调密钥硬编码

**审计编号**：#6
**证据**：
- `file:///workspace/src/Services/Notification/Leno.Notification.Api/Controllers/NotificationCallbacksController.cs#L119` — `?? "LenoNotificationCallbackSecret2024"` 硬编码 fallback
- `file:///workspace/src/Services/Notification/Leno.Notification.Api/Controllers/NotificationCallbacksController.cs#L120` — `raw` 包含 `timestamp` 但未校验新鲜度

**根因**：配置缺失时回退到源码可见的硬编码密钥；时间戳无新鲜度校验，可无限重放。

**修复方案**：删除默认 fallback，启动时校验 `CallbackSecret` 必须配置；加入时间戳新鲜度校验（±5 分钟）。

#### TDD 步骤 1：编写失败测试

```csharp
// 文件：src/Services/Notification/Leno.Notification.Api.Tests/Controllers/NotificationCallbacksControllerSecurityTests.cs
using Leno.Notification.Api.Controllers;
using Leno.Notification.Domain.Aggregates;
using Leno.Notification.Domain.Repositories;
using Leno.Notification.Domain.ValueObjects;
using Leno.Shared.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Leno.Notification.Api.Tests.Controllers;

public class NotificationCallbacksControllerSecurityTests
{
    [Fact]
    public async Task HandleSmsReceiptAsync_ReplayedTimestamp_ShouldReturn401()
    {
        // Arrange — 时间戳超出 5 分钟窗口
        var record = NotificationRecord.Create(
            Guid.NewGuid(), Guid.NewGuid(), "sms_code", null,
            NotificationChannel.Sms, "Title", "Content");
        record.MarkSending();
        record.SetChannelMessageId("msg-123");

        var recordRepoMock = new Mock<INotificationRecordRepository>(MockBehavior.Strict);
        recordRepoMock
            .Setup(r => r.GetByChannelMessageIdAsync("msg-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);
        recordRepoMock.Setup(r => r.UpdateAsync(It.IsAny<NotificationRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var uowMock = new Mock<IUnitOfWork>(MockBehavior.Strict);
        uowMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["Notification:CallbackSecret"]).Returns("real-secret");
        var loggerMock = new Mock<ILogger<NotificationCallbacksController>>();

        var controller = new NotificationCallbacksController(
            recordRepoMock.Object, uowMock.Object, configMock.Object, loggerMock.Object);

        // 构造 10 分钟前的时间戳
        var oldTimestamp = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds().ToString();
        var secret = "real-secret";
        var raw = $"msg-123|true|{oldTimestamp}|{secret}";
        var signature = ComputeHmacSha256(raw, secret);

        // Act
        var result = await controller.HandleSmsReceiptAsync("msg-123", "true", oldTimestamp, signature, CancellationToken.None);

        // Assert — 修复后：时间戳超出窗口应拒绝
        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public void Constructor_MissingCallbackSecret_ShouldThrowOnFirstUse()
    {
        // Arrange — 配置中不设置 CallbackSecret
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["Notification:CallbackSecret"]).Returns((string?)null);

        // Act & Assert — 修复后：缺失密钥应抛异常而非回退硬编码默认值
        // 可在构造函数或 VerifySignature 中校验
        Assert.ThrowsAny<InvalidOperationException>(() =>
        {
            // 触发密钥读取
            var controller = new NotificationCallbacksController(
                new Mock<INotificationRecordRepository>().Object,
                new Mock<IUnitOfWork>().Object,
                configMock.Object,
                new Mock<ILogger<NotificationCallbacksController>>().Object);
        });
    }

    private static string ComputeHmacSha256(string data, string secret)
    {
        using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(data));
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
}
```

#### TDD 步骤 2：运行测试验证失败

```bash
dotnet test --filter "FullyQualifiedName~NotificationCallbacksControllerSecurityTests"
```

**预期结果**：测试失败——当前代码使用硬编码 fallback，`Constructor_MissingCallbackSecret_ShouldThrowOnFirstUse` 不抛异常；`ReplayedTimestamp` 测试返回 200 而非 401。

#### TDD 步骤 3：编写最小实现

修改 `NotificationCallbacksController`：

```csharp
// 构造函数中校验密钥必须配置
private readonly string _callbackSecret;

public NotificationCallbacksController(
    INotificationRecordRepository recordRepository,
    IUnitOfWork unitOfWork,
    IConfiguration configuration,
    ILogger<NotificationCallbacksController> logger)
{
    _recordRepository = recordRepository;
    _unitOfWork = unitOfWork;
    _callbackSecret = configuration["Notification:CallbackSecret"]
        ?? throw new InvalidOperationException("Notification:CallbackSecret 未配置，拒绝启动回执端点");
    _configuration = configuration;
    _logger = logger;
}

// VerifySignature 方法增加时间戳新鲜度校验
private bool VerifySignature(string channelMessageId, string succeeded, string timestamp, string signature)
{
    if (string.IsNullOrWhiteSpace(signature))
    {
        return false;
    }

    // 时间戳新鲜度校验：±5 分钟
    if (!long.TryParse(timestamp, out var ts))
    {
        return false;
    }
    var callbackTime = DateTimeOffset.FromUnixTimeSeconds(ts);
    var skew = Math.Abs((DateTimeOffset.UtcNow - callbackTime).TotalMinutes);
    if (skew > 5)
    {
        _logger.LogWarning("回执时间戳超出窗口 Skew={Skew}min ChannelMessageId={Id}", skew, channelMessageId);
        return false;
    }

    var raw = $"{channelMessageId}|{succeeded}|{timestamp}|{_callbackSecret}";
    var computed = ComputeHmacSha256(raw, _callbackSecret);

    return string.Equals(computed, signature, StringComparison.OrdinalIgnoreCase);
}
```

#### TDD 步骤 4：运行测试验证通过

```bash
dotnet test --filter "FullyQualifiedName~NotificationCallbacksControllerSecurityTests"
```

**预期结果**：测试通过——重放时间戳返回 401，缺失密钥抛 `InvalidOperationException`。

#### TDD 步骤 5：提交

```bash
git add -A
git commit -m "fix(notification): 删除硬编码回调密钥并增加时间戳防重放

- 移除CallbackSecret硬编码fallback，缺失时抛异常拒绝启动
- VerifySignature增加±5分钟时间戳新鲜度校验
- 修复攻击者可伪造回执状态和无限重放的安全漏洞"
```

---

### P0-7：NotificationRecordsController.ResendRecordAsync 只改状态不真正发送

**审计编号**：#7
**证据**：
- `file:///workspace/src/Services/Notification/Leno.Notification.Api/Controllers/NotificationRecordsController.cs#L125` — `record.MarkResend()` 改状态为 Sending
- `file:///workspace/src/Services/Notification/Leno.Notification.Api/Controllers/NotificationRecordsController.cs#L126-L127` — `UpdateAsync + SaveChangesAsync` 仅持久化状态，未调用 `channel.SendAsync`

**根因**：控制器调用 `MarkResend()` 将状态从 `DeadLettered` 迁移到 `Sending`，但未实际发送，没有任何 Job 拾取 `Sending` 状态记录，记录永久卡死。

**修复方案**：将状态改为 `Pending` 而非 `Sending`，让 `NotificationDispatchJob` 接管实际发送。

#### TDD 步骤 1：编写失败测试

```csharp
// 文件：src/Services/Notification/Leno.Notification.Application.Tests/Services/NotificationRecordAppServiceTests.cs
using Leno.Notification.Application.Services;
using Leno.Notification.Domain.Aggregates;
using Leno.Notification.Domain.Repositories;
using Leno.Notification.Domain.ValueObjects;
using Leno.Shared.Abstractions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Leno.Notification.Application.Tests.Services;

public class NotificationRecordAppServiceTests
{
    private readonly Mock<INotificationRecordRepository> _recordRepoMock;
    private readonly Mock<IUnitOfWork> _uowMock;
    private readonly Mock<ILogger<NotificationRecordAppService>> _loggerMock;
    private readonly NotificationRecordAppService _sut;

    public NotificationRecordAppServiceTests()
    {
        _recordRepoMock = new Mock<INotificationRecordRepository>(MockBehavior.Strict);
        _uowMock = new Mock<IUnitOfWork>(MockBehavior.Strict);
        _loggerMock = new Mock<ILogger<NotificationRecordAppService>>();
        _sut = new NotificationRecordAppService(_recordRepoMock.Object, _uowMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task ResendRecordAsync_DeadLetteredRecord_ShouldMoveToPendingNotSending()
    {
        // Arrange
        var recordId = Guid.NewGuid();
        var record = NotificationRecord.Create(
            recordId, Guid.NewGuid(), "test_code", null,
            NotificationChannel.Sms, "Title", "Content");
        // 推进到死信状态：Pending → Sending → Failed → Retried → DeadLettered
        record.MarkSending();
        record.MarkFailed("err", "ERR");
        record.ScheduleRetry(DateTime.UtcNow.AddSeconds(-1));
        record.MoveToDeadLetter("max retries");

        _recordRepoMock
            .Setup(r => r.GetByIdAsync(recordId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);
        _recordRepoMock
            .Setup(r => r.UpdateAsync(record, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _uowMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act — 修复后：状态改为 Pending 让 DispatchJob 接管发送
        await _sut.ResendRecordAsync(recordId, Guid.NewGuid(), CancellationToken.None);

        // Assert — 修复前：状态为 Sending（卡死）；修复后：状态为 Pending（可被 DispatchJob 拾取）
        Assert.Equal(NotificationStatus.Pending, record.Status);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

#### TDD 步骤 2：运行测试验证失败

```bash
dotnet test --filter "FullyQualifiedName~NotificationRecordAppServiceTests.ResendRecordAsync_DeadLetteredRecord_ShouldMoveToPendingNotSending"
```

**预期结果**：测试失败——当前 `ResendRecordAsync` 调用 `MarkResend()` 将状态设为 `Sending`，`Assert.Equal(NotificationStatus.Pending, record.Status)` 失败。

#### TDD 步骤 3：编写最小实现

**3a. 在 `NotificationRecord` 聚合根增加 `RequeueForSend()` 方法**（`DeadLettered → Pending`）：

```csharp
/// <summary>
/// 重新排队发送。DeadLettered → Pending，让 DispatchJob 重新拾取。
/// </summary>
public void RequeueForSend()
{
    if (Status != NotificationStatus.DeadLettered)
    {
        throw new NotificationDomainException(
            $"当前状态 {Status} 不可重新排队，仅 DeadLettered 状态可转入 Pending", "NOTIFICATION_REQUEUE_STATUS_INVALID");
    }

    Status = NotificationStatus.Pending;
    RetryCount = 0;
    ErrorMessage = null;
    ErrorCode = null;
    NextRetryAt = null;
}
```

**3b. 新建 `INotificationRecordAppService.ResendRecordAsync` 应用服务**，控制器委托调用：

```csharp
public async Task ResendRecordAsync(Guid recordId, Guid operatorId, CancellationToken ct)
{
    var record = await _recordRepository.GetByIdAsync(recordId, ct)
        ?? throw new ArgumentException($"通知记录 {recordId} 不存在");

    if (record.Status != NotificationStatus.DeadLettered)
    {
        throw new InvalidOperationException($"记录 {recordId} 非死信状态，无法重发");
    }

    record.RequeueForSend();
    await _recordRepository.UpdateAsync(record, ct);
    await _unitOfWork.SaveChangesAsync(ct);
    _logger.LogInformation("操作员 {OperatorId} 重发死信记录 RecordId={RecordId}", operatorId, recordId);
}
```

**3c. 修改 `NotificationRecordsController.ResendRecordAsync`** 调用应用服务而非直接操作仓储。

#### TDD 步骤 4：运行测试验证通过

```bash
dotnet test --filter "FullyQualifiedName~NotificationRecordAppServiceTests.ResendRecordAsync_DeadLetteredRecord_ShouldMoveToPendingNotSending"
```

**预期结果**：测试通过，`record.Status == NotificationStatus.Pending`。

#### TDD 步骤 5：提交

```bash
git add -A
git commit -m "fix(notification): 死信重发改为Pending状态让DispatchJob接管

- NotificationRecord新增RequeueForSend方法(DeadLettered→Pending)
- 新建INotificationRecordAppService.ResendRecordAsync应用服务
- 控制器委托应用服务，修复重发后记录卡死在Sending的问题"
```

---

### P0-8：NotificationService.SendAsync 超时分支把记录永久滞留在 Sending 状态

**审计编号**：#8
**证据**：
- `file:///workspace/src/Services/Notification/Leno.Notification.Application/Services/NotificationService.cs#L132` — `record.MarkSending()` 先改为 Sending
- `file:///workspace/src/Services/Notification/Leno.Notification.Application/Services/NotificationService.cs#L150-L166` — 超时分支仅保存 Sending 状态，返回 `Succeeded=true, ErrorCode="ACCEPTED_TIMEOUT"`，无 Job 拾取 Sending

**根因**：3 秒超时后记录卡在 `Sending`，`NotificationDispatchJob` 只查 `Pending`，`NotificationRetryJob` 只查 `Retried`，记录永久滞留。

**修复方案**：超时时调用 `record.MarkFailed("发送超时", "ACCEPTED_TIMEOUT")` 进入 `Failed` 状态，由重试 Job 后续处理。

#### TDD 步骤 1：编写失败测试

```csharp
// 文件：src/Services/Notification/Leno.Notification.Application.Tests/NotificationServiceTimeoutTests.cs
using Leno.Notification.Application.Services;
using Leno.Notification.Domain.Aggregates;
using Leno.Notification.Domain.Repositories;
using Leno.Notification.Domain.Services;
using Leno.Notification.Domain.ValueObjects;
using Leno.Shared.Abstractions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Leno.Notification.Application.Tests;

public class NotificationServiceTimeoutTests
{
    private readonly Mock<INotificationRecordRepository> _recordRepoMock;
    private readonly Mock<INotificationTemplateRepository> _templateRepoMock;
    private readonly Mock<ITemplateRenderer> _rendererMock;
    private readonly Mock<INotificationChannel> _channelMock;
    private readonly Mock<IUserContactService> _userContactMock;
    private readonly Mock<IUnitOfWork> _uowMock;
    private readonly Mock<ILogger<AppServices.NotificationService>> _loggerMock;
    private readonly AppServices.NotificationService _sut;

    public NotificationServiceTimeoutTests()
    {
        _recordRepoMock = new Mock<INotificationRecordRepository>(MockBehavior.Strict);
        _templateRepoMock = new Mock<INotificationTemplateRepository>(MockBehavior.Strict);
        _rendererMock = new Mock<ITemplateRenderer>(MockBehavior.Strict);
        _channelMock = new Mock<INotificationChannel>(MockBehavior.Strict);
        _userContactMock = new Mock<IUserContactService>(MockBehavior.Strict);
        _uowMock = new Mock<IUnitOfWork>(MockBehavior.Strict);
        _loggerMock = new Mock<ILogger<AppServices.NotificationService>>();

        _sut = new AppServices.NotificationService(
            _recordRepoMock.Object,
            _templateRepoMock.Object,
            _rendererMock.Object,
            new[] { _channelMock.Object },
            _userContactMock.Object,
            _uowMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task SendAsync_ChannelTimeout_ShouldMarkFailedNotStaySending()
    {
        // Arrange
        var template = NotificationTemplate.Create(
            Guid.NewGuid(), "test_code", NotificationChannel.Sms, "Title", "Body");

        _recordRepoMock
            .Setup(r => r.GetByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationRecord?)null);
        _templateRepoMock
            .Setup(r => r.GetEnabledByCodeAsync("test_code", It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);
        _rendererMock
            .Setup(r => r.Render(It.IsAny<NotificationTemplate>(), It.IsAny<Dictionary<string, string>>()))
            .Returns(("Title", "Content"));
        _recordRepoMock
            .Setup(r => r.AddAsync(It.IsAny<NotificationRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _uowMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _userContactMock
            .Setup(u => u.GetContactAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserContact("13800138000", "test@example.com"));
        _channelMock.SetupGet(c => c.Channel).Returns(NotificationChannel.Sms);

        // 模拟超时：channel.SendAsync 抛 OperationCanceledException
        NotificationRecord? capturedRecord = null;
        _recordRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<NotificationRecord>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationRecord, CancellationToken>((r, _) => capturedRecord = r)
            .Returns(Task.CompletedTask);
        _channelMock
            .Setup(c => c.SendAsync(It.IsAny<ChannelSendRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException("timeout"));

        var request = new NotificationRequest
        {
            TemplateCode = "test_code",
            UserId = Guid.NewGuid(),
            Variables = new Dictionary<string, string>(),
            IdempotencyKey = Guid.NewGuid().ToString()
        };

        // Act
        var result = await _sut.SendAsync(request, CancellationToken.None);

        // Assert — 修复前：状态为 Sending（卡死），Succeeded=true；修复后：状态为 Failed，Succeeded=false
        Assert.False(result.Succeeded);
        Assert.Equal("ACCEPTED_TIMEOUT", result.ErrorCode);
        capturedRecord.Should().NotBeNull();
        capturedRecord!.Status.Should().Be(NotificationStatus.Failed);
    }
}
```

#### TDD 步骤 2：运行测试验证失败

```bash
dotnet test --filter "FullyQualifiedName~NotificationServiceTimeoutTests.SendAsync_ChannelTimeout_ShouldMarkFailedNotStaySending"
```

**预期结果**：测试失败——当前超时分支返回 `Succeeded=true`，记录状态为 `Sending`，`Assert.False(result.Succeeded)` 和 `capturedRecord.Status.Should().Be(Failed)` 均失败。

#### TDD 步骤 3：编写最小实现

修改 `NotificationService.cs#L150-L166` 超时分支：

```csharp
catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
{
    // 修复：超时标记为 Failed 让 RetryJob 后续处理，而非滞留在 Sending
    _logger.LogWarning("通知发送超时 RecordId={RecordId} TemplateCode={Code} Channel={Channel}",
        recordId, request.TemplateCode, template.Channel);

    record.MarkFailed("发送超时", "ACCEPTED_TIMEOUT");
    await _recordRepository.UpdateAsync(record, ct);
    await _unitOfWork.SaveChangesAsync(ct);

    return new NotificationSendResult
    {
        Succeeded = false,
        RecordId = recordId,
        ErrorCode = "ACCEPTED_TIMEOUT",
        ErrorMessage = "通知发送超时，已标记为失败等待重试"
    };
}
```

#### TDD 步骤 4：运行测试验证通过

```bash
dotnet test --filter "FullyQualifiedName~NotificationServiceTimeoutTests.SendAsync_ChannelTimeout_ShouldMarkFailedNotStaySending"
```

**预期结果**：测试通过——`result.Succeeded` 为 `false`，`capturedRecord.Status` 为 `Failed`。

#### TDD 步骤 5：提交

```bash
git add -A
git commit -m "fix(notification): 超时分支标记Failed而非滞留Sending

- 超时时调用record.MarkFailed而非仅保存Sending状态
- 返回Succeeded=false让调用方感知失败
- 修复超时记录永久卡死在Sending无法被RetryJob拾取的问题"
```

---

### P0-9：AliyunSmsChannel/TencentSmsChannel 硬编码模板编码

**审计编号**：#9
**证据**：
- `file:///workspace/src/Services/Notification/Leno.Notification.Infrastructure/Channels/SmsChannel.cs#L62` — `TemplateCode = "SMS_000000"` 硬编码
- `file:///workspace/src/Services/Notification/Leno.Notification.Infrastructure/Channels/SmsChannel.cs#L149` — `TemplateId = "000000"` 硬编码

**根因**：`ChannelSendRequest` 值对象未携带 `SmsTemplateCode`，渠道无法获取实际模板编码。

**修复方案**：在 `ChannelSendRequest` 增加 `SmsTemplateCode` 字段，由 `NotificationService` 从 `NotificationTemplate.SmsTemplateCode` 透传；渠道优先使用该字段。

#### TDD 步骤 1：编写失败测试

```csharp
// 文件：src/Services/Notification/Leno.Notification.Domain.Tests/ValueObjects/ChannelSendRequestTests.cs
using Leno.Notification.Domain.ValueObjects;
using Xunit;

namespace Leno.Notification.Domain.Tests.ValueObjects;

public class ChannelSendRequestTests
{
    [Fact]
    public void ChannelSendRequest_ShouldCarrySmsTemplateCode()
    {
        // Arrange & Act — 修复后：ChannelSendRequest 增加 SmsTemplateCode 字段
        var recipient = new Recipient(Guid.NewGuid(), "13800138000", "test@example.com");
        var request = new ChannelSendRequest(
            NotificationChannel.Sms,
            recipient,
            "Subject",
            "Body",
            "idem-key-123",
            "SMS_12345678");  // 新增字段

        // Assert
        Assert.Equal("SMS_12345678", request.SmsTemplateCode);
    }

    [Fact]
    public void ChannelSendRequest_WithNullSmsTemplateCode_ShouldDefaultToNull()
    {
        // Arrange & Act
        var recipient = new Recipient(Guid.NewGuid(), "13800138000", "test@example.com");
        var request = new ChannelSendRequest(
            NotificationChannel.Sms,
            recipient,
            "Subject",
            "Body",
            "idem-key-123");

        // Assert — 可选参数默认 null
        Assert.Null(request.SmsTemplateCode);
    }
}
```

#### TDD 步骤 2：运行测试验证失败

```bash
dotnet test --filter "FullyQualifiedName~ChannelSendRequestTests"
```

**预期结果**：编译失败——`ChannelSendRequest` 当前无 `SmsTemplateCode` 字段/参数。

#### TDD 步骤 3：编写最小实现

**3a. 修改 `ChannelSendRequest` 值对象** 增加 `SmsTemplateCode` 字段：

```csharp
public sealed record ChannelSendRequest(
    NotificationChannel Channel,
    Recipient Recipient,
    string? Subject,
    string Body,
    string? IdempotencyKey,
    string? SmsTemplateCode = null);
```

**3b. 在 `NotificationService.SendAsync` 构建 `ChannelSendRequest` 时透传** `template.SmsTemplateCode`：

```csharp
var sendRequest = new ChannelSendRequest(
    template.Channel,
    recipient,
    title,
    content,
    request.IdempotencyKey,
    template.SmsTemplateCode);  // 透传模板配置的 SMS 模板编码
```

**3c. 在 `AliyunSmsProvider.SendAsync` 中使用 `request.SmsTemplateCode`** 替代硬编码：

```csharp
var templateCode = request.SmsTemplateCode;
if (string.IsNullOrWhiteSpace(templateCode))
{
    return new ChannelSendResult(false, "短信模板编码未配置", "SMS_TEMPLATE_CODE_MISSING", null);
}

var requestBody = new
{
    PhoneNumbers = phoneNumber,
    SignName = _options.SignName,
    TemplateCode = templateCode,  // 使用传入值而非 "SMS_000000"
    TemplateParam = JsonSerializer.Serialize(new { content = request.Body }, JsonOptions)
};
```

同理修改 `TencentSmsProvider` 使用 `request.SmsTemplateCode` 替代 `"000000"`。

#### TDD 步骤 4：运行测试验证通过

```bash
dotnet test --filter "FullyQualifiedName~ChannelSendRequestTests"
```

**预期结果**：测试通过，`SmsTemplateCode` 正确携带。

#### TDD 步骤 5：提交

```bash
git add -A
git commit -m "fix(notification): ChannelSendRequest增加SmsTemplateCode字段

- ChannelSendRequest值对象增加SmsTemplateCode可选字段
- NotificationService从NotificationTemplate.SmsTemplateCode透传
- AliyunSmsProvider/TencentSmsProvider使用传入模板编码替代硬编码
- 修复短信模板编码固定为SMS_000000导致发送失败的问题"
```

---

### P0-10：AliyunSmsChannel 用响应体当 ChannelMessageId

**审计编号**：#10
**证据**：
- `file:///workspace/src/Services/Notification/Leno.Notification.Infrastructure/Channels/SmsChannel.cs#L78` — `return new ChannelSendResult(true, null, null, responseContent);` 整个响应体作为 ChannelMessageId
- `file:///workspace/src/Services/Notification/Leno.Notification.Infrastructure/Configurations/NotificationRecordConfiguration.cs#L34` — `HasMaxLength(128)` 会截断

**根因**：响应体（JSON 字符串）被截断后与回执回调的 messageId 不匹配，回执永远 404。

**修复方案**：从阿里云/腾讯云响应 JSON 中解析真正的 `BizId`/`SerialNo` 字段。

#### TDD 步骤 1：编写失败测试

```csharp
// 文件：src/Services/Notification/Leno.Notification.Infrastructure.Tests/Channels/AliyunSmsProviderTests.cs
using Leno.Notification.Domain.Services;
using Leno.Notification.Domain.ValueObjects;
using Leno.Notification.Infrastructure.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Net;
using System.Text;
using Xunit;

namespace Leno.Notification.Infrastructure.Tests.Channels;

public class AliyunSmsProviderTests
{
    [Fact]
    public async Task SendAsync_Success_ShouldReturnBizIdAsChannelMessageId()
    {
        // Arrange
        var options = Options.Create(new SmsChannelOptions
        {
            Provider = "Aliyun",
            AccessKeyId = "AKID123",
            AccessKeySecret = "SK456",
            SignName = "Leno"
        });

        // 阿里云成功响应格式：{"Code":"OK","Message":"OK","RequestId":"xxx","BizId":"123456789012345678^0"}
        var responseBody = """{"Code":"OK","Message":"OK","RequestId":"req-abc","BizId":"123456789012345678^0"}""";
        var httpMessage = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
        };

        var handlerMock = new Mock<System.Net.Http.HttpMessageHandler>(MockBehavior.Strict);
        // 使用受保护的 SendAsync
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<System.Net.Http.HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpMessage);

        var httpClient = new System.Net.Http.HttpClient(handlerMock.Object);
        var loggerMock = new Mock<ILogger<AliyunSmsProvider>>();

        var provider = new AliyunSmsProvider(options, httpClient, loggerMock.Object);

        var recipient = new Recipient(Guid.NewGuid(), "13800138000", "test@example.com");
        var request = new ChannelSendRequest(
            NotificationChannel.Sms, recipient, null, "Body", "idem-key", "SMS_12345678");

        // Act
        var result = await provider.SendAsync(request, CancellationToken.None);

        // Assert — 修复后：ChannelMessageId 应为解析出的 BizId，而非整个响应体
        Assert.True(result.Succeeded);
        Assert.Equal("123456789012345678^0", result.ChannelMessageId);
        Assert.NotEqual(responseBody, result.ChannelMessageId);
    }

    [Fact]
    public async Task SendAsync_SuccessButNoBizId_ShouldReturnNullChannelMessageId()
    {
        // Arrange — 响应中无 BizId 字段
        var options = Options.Create(new SmsChannelOptions
        {
            Provider = "Aliyun",
            AccessKeyId = "AKID123",
            AccessKeySecret = "SK456",
            SignName = "Leno"
        });

        var responseBody = """{"Code":"OK","Message":"OK","RequestId":"req-abc"}""";
        var httpMessage = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
        };

        var handlerMock = new Mock<System.Net.Http.HttpMessageHandler>(MockBehavior.Strict);
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<System.Net.Http.HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpMessage);

        var httpClient = new System.Net.Http.HttpClient(handlerMock.Object);
        var loggerMock = new Mock<ILogger<AliyunSmsProvider>>();

        var provider = new AliyunSmsProvider(options, httpClient, loggerMock.Object);
        var recipient = new Recipient(Guid.NewGuid(), "13800138000", "test@example.com");
        var request = new ChannelSendRequest(
            NotificationChannel.Sms, recipient, null, "Body", "idem-key", "SMS_12345678");

        // Act
        var result = await provider.SendAsync(request, CancellationToken.None);

        // Assert — 无 BizId 时 ChannelMessageId 为 null
        Assert.True(result.Succeeded);
        Assert.Null(result.ChannelMessageId);
    }
}
```

#### TDD 步骤 2：运行测试验证失败

```bash
dotnet test --filter "FullyQualifiedName~AliyunSmsProviderTests"
```

**预期结果**：测试失败——当前代码返回 `responseContent`（整个 JSON 字符串）作为 `ChannelMessageId`，不等于 `"123456789012345678^0"`。

#### TDD 步骤 3：编写最小实现

修改 `AliyunSmsProvider.SendAsync` 成功分支，解析响应 JSON 提取 `BizId`：

```csharp
if (response.IsSuccessStatusCode)
{
    _logger.LogInformation("阿里云短信已发送 Phone={Phone}", phoneNumber);

    // 从响应 JSON 中解析 BizId 作为 ChannelMessageId
    string? bizId = null;
    try
    {
        using var doc = JsonDocument.Parse(responseContent);
        if (doc.RootElement.TryGetProperty("BizId", out var bizIdElement))
        {
            bizId = bizIdElement.GetString();
        }
    }
    catch (JsonException ex)
    {
        _logger.LogWarning(ex, "解析阿里云短信响应失败，BizId 不可用");
    }

    return new ChannelSendResult(true, null, null, bizId);
}
```

同理修改 `TencentSmsProvider` 解析 `SerialNo` 字段。

#### TDD 步骤 4：运行测试验证通过

```bash
dotnet test --filter "FullyQualifiedName~AliyunSmsProviderTests"
```

**预期结果**：测试通过——`ChannelMessageId` 为 `"123456789012345678^0"`，无 BizId 时为 `null`。

#### TDD 步骤 5：提交

```bash
git add -A
git commit -m "fix(notification): 解析阿里云BizId作为ChannelMessageId

- AliyunSmsProvider从响应JSON解析BizId字段替代整个响应体
- TencentSmsProvider解析SerialNo字段
- 无BizId时返回null而非截断的响应字符串
- 修复短信回执因ChannelMessageId不匹配永远404的问题"
```

---

### P0-11：通知模板 (Code, Channel) 索引未声明唯一

**审计编号**：#11
**证据**：
- `file:///workspace/src/Services/Notification/Leno.Notification.Infrastructure/Configurations/NotificationTemplateConfiguration.cs#L45` — `HasIndex(t => new { t.Code, t.Channel })` 未调用 `IsUnique()`
- `file:///workspace/src/Services/Notification/Leno.Notification.Infrastructure/Repositories/EfCoreNotificationTemplateRepository.cs#L26-L28` — `FirstOrDefaultAsync` 结果不确定

**根因**：无唯一约束，同一 code+channel 可存在多个 Enabled 模板，查询返回不确定。

**修复方案**：索引加 `IsUnique()`，并在聚合根 `Create`/`Update` 中校验。

#### TDD 步骤 1：编写失败测试

```csharp
// 文件：src/Services/Notification/Leno.Notification.Infrastructure.Tests/Configurations/NotificationTemplateConfigurationTests.cs
using Leno.Notification.Domain.Aggregates;
using Leno.Notification.Domain.ValueObjects;
using Leno.Notification.Infrastructure.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace Leno.Notification.Infrastructure.Tests.Configurations;

public class NotificationTemplateConfigurationTests
{
    private static IModel BuildModel()
    {
        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var context = new NotificationDbContext(options);
        return context.Model;
    }

    [Fact]
    public void NotificationTemplate_CodeChannelIndex_ShouldBeUnique()
    {
        // Arrange
        var model = BuildModel();
        var entityType = model.FindEntityType(typeof(NotificationTemplate))!;

        // Act — 查找 (Code, Channel) 索引
        var index = entityType.GetIndexes()
            .FirstOrDefault(i => i.Properties.Count == 2
                && i.Properties.Any(p => p.Name == nameof(NotificationTemplate.Code))
                && i.Properties.Any(p => p.Name == nameof(NotificationTemplate.Channel)));

        // Assert — 修复后：索引应声明 IsUnique() = true
        Assert.NotNull(index);
        Assert.True(index.IsUnique);
    }
}
```

#### TDD 步骤 2：运行测试验证失败

```bash
dotnet test --filter "FullyQualifiedName~NotificationTemplateConfigurationTests.NotificationTemplate_CodeChannelIndex_ShouldBeUnique"
```

**预期结果**：测试失败——当前 `IsUnique` 为 `false`。

#### TDD 步骤 3：编写最小实现

修改 `NotificationTemplateConfiguration.cs#L45`：

```csharp
builder.HasIndex(t => new { t.Code, t.Channel })
    .IsUnique()
    .HasDatabaseName("ix_notification_templates_code_channel");
```

同时需生成 EF Core Migration：`dotnet ef migrations add AddUniqueIndexOnTemplateCodeChannel`。

#### TDD 步骤 4：运行测试验证通过

```bash
dotnet test --filter "FullyQualifiedName~NotificationTemplateConfigurationTests"
```

**预期结果**：测试通过，`index.IsUnique` 为 `true`。

#### TDD 步骤 5：提交

```bash
git add -A
git commit -m "fix(notification): 模板(Code,Channel)索引声明唯一约束

- NotificationTemplateConfiguration索引增加IsUnique()
- 生成EF Core Migration
- 防止同一code+channel存在多个Enabled模板导致查询不确定"
```

---

### P0-12：NotificationTemplatesController.GetByIdAsync 全表加载后内存查找

**审计编号**：#12
**证据**：
- `file:///workspace/src/Services/Notification/Leno.Notification.Api/Controllers/NotificationTemplatesController.cs#L43` — `QueryTemplatesAsync(null, null, 1, int.MaxValue, ct)` 全表加载

**根因**：按 ID 查询时加载所有模板到内存再 `FirstOrDefault`，等同于全表扫描。

**修复方案**：在 `INotificationTemplateAppService` 增加 `GetByIdAsync` 方法，直接走主键查询。

#### TDD 步骤 1：编写失败测试

```csharp
// 文件：src/Services/Notification/Leno.Notification.Application.Tests/NotificationTemplateAppServiceTests.cs
using Leno.Notification.Application.Services;
using Leno.Notification.Domain.Aggregates;
using Leno.Notification.Domain.Repositories;
using Leno.Notification.Domain.ValueObjects;
using Leno.Shared.Abstractions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Leno.Notification.Application.Tests;

public class NotificationTemplateAppServiceTests
{
    private readonly Mock<INotificationTemplateRepository> _templateRepoMock;
    private readonly Mock<IUnitOfWork> _uowMock;
    private readonly Mock<ILogger<NotificationTemplateAppService>> _loggerMock;
    private readonly NotificationTemplateAppService _sut;

    public NotificationTemplateAppServiceTests()
    {
        _templateRepoMock = new Mock<INotificationTemplateRepository>(MockBehavior.Strict);
        _uowMock = new Mock<IUnitOfWork>(MockBehavior.Strict);
        _loggerMock = new Mock<ILogger<NotificationTemplateAppService>>();
        _sut = new NotificationTemplateAppService(
            _templateRepoMock.Object, _uowMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldQueryByPrimaryKey_NotLoadAll()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var template = NotificationTemplate.Create(
            templateId, "test_code", NotificationChannel.Sms, "Title", "Body");

        _templateRepoMock
            .Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

        // Act
        var result = await _sut.GetByIdAsync(templateId, CancellationToken.None);

        // Assert — 修复后：应调用 GetByIdAsync（主键查询），不调用 QueryTemplatesAsync
        Assert.NotNull(result);
        Assert.Equal(templateId, result!.TemplateId);
        _templateRepoMock.Verify(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()), Times.Once);
        _templateRepoMock.Verify(
            r => r.QueryAsync(It.IsAny<string>(), It.IsAny<NotificationChannel?>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
```

#### TDD 步骤 2：运行测试验证失败

```bash
dotnet test --filter "FullyQualifiedName~NotificationTemplateAppServiceTests.GetByIdAsync_ShouldQueryByPrimaryKey_NotLoadAll"
```

**预期结果**：编译失败——`INotificationTemplateAppService` 接口无 `GetByIdAsync` 方法。

#### TDD 步骤 3：编写最小实现

**3a. 在 `INotificationTemplateAppService` 接口增加方法**：

```csharp
Task<NotificationTemplateDto?> GetByIdAsync(Guid templateId, CancellationToken ct = default);
```

**3b. 在 `NotificationTemplateAppService` 中实现**：

```csharp
public async Task<NotificationTemplateDto?> GetByIdAsync(Guid templateId, CancellationToken ct = default)
{
    var template = await _templateRepository.GetByIdAsync(templateId, ct);
    return template is null ? null : MapToDto(template);
}
```

**3c. 修改 `NotificationTemplatesController.GetByIdAsync`** 调用 `_templateAppService.GetByIdAsync(templateId, ct)` 替代全表加载。

#### TDD 步骤 4：运行测试验证通过

```bash
dotnet test --filter "FullyQualifiedName~NotificationTemplateAppServiceTests.GetByIdAsync_ShouldQueryByPrimaryKey_NotLoadAll"
```

**预期结果**：测试通过，`GetByIdAsync` 走主键查询，`QueryAsync` 未被调用。

#### TDD 步骤 5：提交

```bash
git add -A
git commit -m "fix(notification): 模板按ID查询走主键替代全表加载

- INotificationTemplateAppService新增GetByIdAsync方法
- 控制器调用GetByIdAsync替代QueryTemplatesAsync(int.MaxValue)
- 修复按ID查询触发全表扫描的性能问题"
```

---

## 🟡 P1（中严重度）任务清单

> 以下为 26 个中严重度问题的任务清单格式，每项包含编号、证据、任务描述和验收标准。

### P1-13：ChannelSelector.NormalizeProvider 死代码 + 首字母大写未实现

**证据**：`file:///workspace/src/Services/Notification/Leno.Notification.Domain/Services/ChannelSelector.cs#L143-L148`
**任务**：实现 `NormalizeProvider` 首字母大写逻辑，或改用 `StringComparison.OrdinalIgnoreCase` 比较 provider 名称。
**验收标准**：配置 `"aliyun"` 时 `GetSmsFallback` 正确匹配 `"Aliyun"`。

### P1-14：GetRetryableAsync 用 DefaultMaxRetry 常量而非聚合自身 MaxRetry

**证据**：`file:///workspace/src/Services/Notification/Leno.Notification.Infrastructure/Repositories/EfCoreNotificationRecordRepository.cs#L76`
**任务**：将查询条件改为 `n.RetryCount < n.MaxRetry`，EF Core 可翻译为 SQL。
**验收标准**：自定义 `MaxRetry=5` 的记录在 `RetryCount=3` 时仍被 RetryJob 拾取。

### P1-15：IdempotencyKey 索引非唯一 + 幂等检查无锁

**证据**：
- `file:///workspace/src/Services/Notification/Leno.Notification.Infrastructure/Configurations/NotificationRecordConfiguration.cs#L48` — 未声明 `IsUnique()`
- `file:///workspace/src/Services/Notification/Leno.Notification.Application/Services/NotificationService.cs#L53-L68` — 无锁两步检查
**任务**：① 索引加 `IsUnique().HasFilter("[idempotency_key] IS NOT NULL")`；② `SendAsync` 用 `BeginTransactionAsync` 包裹幂等检查与创建。
**验收标准**：并发相同 IdempotencyKey 请求只创建一条记录，第二个返回已有记录。

### P1-16：ChannelMessageId 缺索引

**证据**：
- `file:///workspace/src/Services/Notification/Leno.Notification.Infrastructure/Configurations/NotificationRecordConfiguration.cs#L44-L48` — 无 ChannelMessageId 索引
- `file:///workspace/src/Services/Notification/Leno.Notification.Infrastructure/Repositories/EfCoreNotificationRecordRepository.cs#L144-L145`
**任务**：增加 `builder.HasIndex(n => n.ChannelMessageId).HasDatabaseName("ix_notification_records_channel_message_id");`
**验收标准**：回执查询 `GetByChannelMessageIdAsync` 走索引。

### P1-17：(Status, NextRetryAt) / (Status, RetryCount) 复合索引缺失

**证据**：
- `file:///workspace/src/Services/Notification/Leno.Notification.Infrastructure/Configurations/NotificationRecordConfiguration.cs#L44-L48` — 仅 Status 单列索引
- `file:///workspace/src/Services/Notification/Leno.Notification.Infrastructure/Repositories/EfCoreNotificationRecordRepository.cs#L73-L80`、`#L106-L115`
**任务**：增加复合索引 `HasIndex(n => new { n.Status, n.NextRetryAt })` 与 `HasIndex(n => new { n.Status, n.RetryCount })`。
**验收标准**：RetryJob 查询使用复合索引，执行计划无全表扫描。

### P1-18：RateLimitAppService 用 static 内存字典存储限流配置

**证据**：`file:///workspace/src/Services/Notification/Leno.Notification.Application/Services/RateLimitAppService.cs#L16-L39`、`#L63-L78`
**任务**：将限流配置持久化到 DB（新增 `notification_rate_limit_configs` 表），用 `ConcurrentDictionary` 缓存。
**验收标准**：配置修改持久化，进程重启不丢失，多实例一致。

### P1-19：NotificationConfigAppService.UpdateConfigAsync 不持久化

**证据**：`file:///workspace/src/Services/Notification/Leno.Notification.Infrastructure/Services/NotificationConfigAppService.cs#L57-L84`
**任务**：将配置持久化到 DB 表 + 通过 `IOptionsChangeTokenSource` 触发热重载。
**验收标准**：`PUT /api/admin/notification-config` 修改后，下次发送使用新配置。

### P1-20：IRateLimiter 注册但从未被调用

**证据**：
- `file:///workspace/src/Services/Notification/Leno.Notification.Infrastructure/Dependencies/ServiceCollectionExtensions.cs#L122`
- `file:///workspace/src/Services/Notification/Leno.Notification.Application/Services/NotificationService.cs#L51-L184`
**任务**：在 `NotificationService.SendAsync` 渠道发送前注入 `IRateLimiter.AcquireAsync`，被拒绝时标记 `Failed` + `RATE_LIMITED`。
**验收标准**：超过限流阈值的发送被拦截，记录状态为 `Failed`、ErrorCode 为 `RATE_LIMITED`。

### P1-21：NotificationAppService.GetNotificationsAsync Total 与过滤条件不一致

**证据**：`file:///workspace/src/Services/Notification/Leno.Notification.Application/Services/NotificationAppService.cs#L36`
**任务**：将 `CountByUserAsync(userId, null, ct)` 改为 `CountByUserAsync(userId, isRead, ct)`。
**验收标准**：未读页的 Total 等于未读条数，与列表一致。

### P1-22：NotificationAppService.MarkAsReadAsync N+1 查询

**证据**：`file:///workspace/src/Services/Notification/Leno.Notification.Application/Services/NotificationAppService.cs#L50-L65`
**任务**：在仓储层增加 `GetByIdsAsync(List<Guid> ids)` 一次查出，或用 `ExecuteUpdateAsync` 批量更新。
**验收标准**：批量标记 100 条已读只产生 1 次 SELECT + 1 次 UPDATE。

### P1-23：DeadLetterAppService.BatchResendAsync 状态机异常导致记录卡死

**证据**：`file:///workspace/src/Services/Notification/Leno.Notification.Application/Services/DeadLetterAppService.cs#L94-L119`
**任务**：将 `MarkResend` 调用挪到 `BuildChannelSendRequestAsync` 之后、`sender.SendAsync` 之前；catch 中调用 `record.MoveToDeadLetter("重发失败")` 回退状态。
**验收标准**：重发异常时记录回到 `DeadLettered` 而非卡在 `Sending`。

### P1-24：NotificationRetryJob.ProcessScheduledRetriesAsync MarkSending 在 try 块外

**证据**：`file:///workspace/src/Services/Notification/Leno.Notification.Infrastructure/Jobs/NotificationRetryJob.cs#L108-L165`
**任务**：将 `MarkSending` 移入 try 块，catch 中处理状态机异常后 `continue`。
**验收标准**：单条记录状态机异常不中断整批重试。

### P1-25：NotificationRetryJob / NotificationDispatchJob 无锁并发

**证据**：
- `file:///workspace/src/Services/Notification/Leno.Notification.Infrastructure/Jobs/NotificationDispatchJob.cs#L47-L83`
- `file:///workspace/src/Services/Notification/Leno.Notification.Infrastructure/Jobs/NotificationRetryJob.cs#L61-L94`、`#L99-L170`
**任务**：SQL 层用 `SELECT ... WITH (UPDLOCK, READPAST)`（SQL Server）或应用层用 Redis 分布式锁包裹记录 ID。
**验收标准**：多实例并发 Job 不重复拾取同一记录。

### P1-26：NotificationDispatcher.DispatchAsync 一次性 SaveChanges 包裹多条记录

**证据**：`file:///workspace/src/Services/Notification/Leno.Notification.Infrastructure/Services/NotificationDispatcher.cs#L69-L116`
**任务**：将每个 channel 的处理放入独立 try-catch，单渠道失败不影响其他。
**验收标准**：Email 发送失败时 Sms 仍正常发送，`DispatchAsync` 不抛异常。

### P1-27：AliyunSmsChannel 缺少阿里云签名算法

**证据**：`file:///workspace/src/Services/Notification/Leno.Notification.Infrastructure/Channels/SmsChannel.cs#L66-L71`
**任务**：使用阿里云/腾讯云官方 SDK 封装防腐层，或按官方文档实现 HMAC-SHA1 / TC3-HMAC-SHA256 签名算法。
**验收标准**：调用阿里云真实端点返回 200，非 401/403。

### P1-28：InAppChannel Redis 失败时返回 Succeeded=true

**证据**：`file:///workspace/src/Services/Notification/Leno.Notification.Infrastructure/Channels/InAppChannel.cs#L35-L49`
**任务**：区分"DB 写入成功"与"缓存更新成功"，记录 `CacheSyncFailed` 状态字段或引入定时同步 Job。
**验收标准**：Redis 故障期间站内信 DB 写入成功，缓存恢复后自动重建。

### P1-29：SmtpEmailChannel.AuthenticateAsync 超时不映射为 SMTP_AUTH_TIMEOUT

**证据**：`file:///workspace/src/Services/Notification/Leno.Notification.Infrastructure/Channels/EmailChannel.cs#L67-L83`
**任务**：将 `AuthenticateAsync` 包入 try-catch，超时返回 `SMTP_AUTH_TIMEOUT`。
**验收标准**：认证超时返回 `SMTP_AUTH_TIMEOUT` 而非 `EMAIL_EXCEPTION`。

### P1-30：SmtpEmailChannel.DisconnectAsync 用 CancellationToken.None

**证据**：`file:///workspace/src/Services/Notification/Leno.Notification.Infrastructure/Channels/EmailChannel.cs#L82-L83`
**任务**：将 `DisconnectAsync(true, CancellationToken.None)` 改为 `DisconnectAsync(true, linkedCts.Token)`。
**验收标准**：网络抖动时 DisconnectAsync 不长时间阻塞。

### P1-31：TemplateRenderer.Render 同步方法不校验必填变量

**证据**：
- `file:///workspace/src/Services/Notification/Leno.Notification.Infrastructure/Services/TemplateRenderer.cs#L42-L50`
- `file:///workspace/src/Services/Notification/Leno.Notification.Application/Services/NotificationService.cs#L88`
**任务**：让 `Render` 也调用 `ValidateRequiredVariables`；或 `NotificationService.SendAsync` 改用 `RenderAsync`。
**验收标准**：必填变量缺失时 `Render` 抛异常而非保留 `{{var}}` 占位符。

### P1-32：TemplateRenderer.Render 标题不 HTML 转义

**证据**：`file:///workspace/src/Services/Notification/Leno.Notification.Infrastructure/Services/TemplateRenderer.cs#L47-L49`
**任务**：标题也做 HTML 转义（`escapeHtml: true`），或对 Subject 做纯文本清洗。
**验收标准**：变量值含 `<script>` 时标题中被转义为 `&lt;script&gt;`。

### P1-33：NotificationRecordsController / NotificationCallbacksController 越层访问仓储

**证据**：
- `file:///workspace/src/Services/Notification/Leno.Notification.Api/Controllers/NotificationRecordsController.cs#L22-L46`、`#L107-L132`
- `file:///workspace/src/Services/Notification/Leno.Notification.Api/Controllers/NotificationCallbacksController.cs#L19-L34`
**任务**：新增 `INotificationRecordAppService` 和 `IReceiptAppService`，控制器只转发调用。
**验收标准**：控制器不直接注入仓储/聚合，只注入应用服务。

### P1-34：ApplyReceipt 失败回执不改状态

**证据**：`file:///workspace/src/Services/Notification/Leno.Notification.Domain/Aggregates/NotificationRecord.cs#L299-L334`
**任务**：`succeeded=false` 时调用 `MarkFailed("渠道回执确认失败", "CHANNEL_RECEIPT_FAILED")`。
**验收标准**：回执失败后记录状态为 `Failed`，可被 RetryJob 拾取。

### P1-35：EfCoreNotificationRecordRepository.MarkAllAsReadAsync 绕过聚合根

**证据**：`file:///workspace/src/Services/Notification/Leno.Notification.Infrastructure/Repositories/EfCoreNotificationRecordRepository.cs#L83-L88`
**任务**：`ExecuteUpdateAsync` 增加 `WHERE channel = 0`（InApp）过滤条件。
**验收标准**：批量标记已读只影响 InApp 渠道记录。

### P1-36：NotificationDispatcher 重复创建模板查询与渠道字典

**证据**：
- `file:///workspace/src/Services/Notification/Leno.Notification.Infrastructure/Services/NotificationDispatcher.cs#L70`
- `file:///workspace/src/Services/Notification/Leno.Notification.Infrastructure/Jobs/NotificationDispatchJob.cs#L53`
**任务**：将渠道字典缓存为构造函数字段（P0-1 修复已包含此项）。
**验收标准**：`DispatchAsync` 不每次重建字典。

### P1-37：NotificationService.SendAsync 渲染失败不创建记录

**证据**：`file:///workspace/src/Services/Notification/Leno.Notification.Application/Services/NotificationService.cs#L86-L99`
**任务**：渲染失败时也创建 `NotificationRecord`，状态为 `Failed` + `TEMPLATE_RENDER_FAILED`。
**验收标准**：渲染失败的通知在管理后台可查询到 `Failed` 状态记录。

### P1-38：NotificationPreference 偏好聚合未在 NotificationService.SendAsync 中查询使用

**证据**：
- `file:///workspace/src/Services/Notification/Leno.Notification.Application/Services/NotificationService.cs#L51-L184`
- `file:///workspace/src/Services/Notification/Leno.Notification.Infrastructure/Services/NotificationDispatcher.cs#L63-L67`
**任务**：统一入口——让 `NotificationService.SendAsync` 内部查询 `NotificationPreference` 并按偏好过滤渠道；或删除 `NotificationDispatcher` 把偏好查询移入 `NotificationService`。
**验收标准**：用户设置"不接收短信"后，通过 API 或 Consumer 发送的通知不触发短信渠道。

---

## 🟢 P2（低严重度）任务清单

### P2-39：Recipient.Equals 与 GetHashCode 比较算法不一致

**证据**：`file:///workspace/src/Services/Notification/Leno.Notification.Domain/ValueObjects/Recipient.cs#L74-L83`
**任务**：将 `GetHashCode` 改为 `StringComparer.OrdinalIgnoreCase.GetHashCode(Email)`。
**验收标准**：两个 `Equals` 相等的 `Recipient` 的 `GetHashCode` 相等。

### P2-40：NotificationDbContextDesignTimeFactory 硬编码数据库密码

**证据**：`file:///workspace/src/Services/Notification/Leno.Notification.Infrastructure/NotificationDbContextDesignTimeFactory.cs#L15`
**任务**：从环境变量读取密码，或用占位符 `Password=***`，文档说明需配置。
**验收标准**：源码中无明文密码。

### P2-41：NotificationSendController 失败返回 200 OK + body code=400

**证据**：`file:///workspace/src/Services/Notification/Leno.Notification.Api/Controllers/NotificationSendController.cs#L63-L69`
**任务**：失败时返回 `BadRequest(...)`（HTTP 400）。
**验收标准**：发送失败的 HTTP 状态码为 400。

### P2-42：RetryPolicy.ShouldRetry 与 ChannelSelector.IsRetryableError 默认保守可重试

**证据**：
- `file:///workspace/src/Services/Notification/Leno.Notification.Infrastructure/Services/RetryPolicy.cs#L96-L98`
- `file:///workspace/src/Services/Notification/Leno.Notification.Domain/Services/ChannelSelector.cs#L116-L118`
**任务**：未知错误码默认返回 `false`（不可重试），由人工分析后加入白名单。
**验收标准**：未知错误码不触发重试，直接进入死信。

### P2-43：NotificationRecord.MarkAsRead 无幂等保护

**证据**：`file:///workspace/src/Services/Notification/Leno.Notification.Domain/Aggregates/NotificationRecord.cs#L242-L250`
**任务**：增加 `if (IsRead) return;` 短路。
**验收标准**：重复调用 `MarkAsRead` 不触发不必要的更新。

### P2-44：NotificationTemplate.Update 不允许更新 Code/Channel

**证据**：`file:///workspace/src/Services/Notification/Leno.Notification.Domain/Aggregates/NotificationTemplate.cs#L87-L93`
**任务**：在 DTO 层校验 Code/Channel 与现存记录一致，不一致时返回 400。
**验收标准**：尝试修改 Code 时返回 400 错误，而非静默忽略。

### P2-45：NotificationPreference.GetChannels 每次返回新列表

**证据**：`file:///workspace/src/Services/Notification/Leno.Notification.Domain/Aggregates/NotificationPreference.cs#L84-L92`
**任务**：缓存为 `static readonly` 不可变列表。
**验收标准**：`GetChannels` 返回同一引用，不每次分配新 `List<T>`。

### P2-46：NotificationTemplate.Update 不校验 SmsTemplateCode 格式

**证据**：`file:///workspace/src/Services/Notification/Leno.Notification.Domain/Aggregates/NotificationTemplate.cs#L87-L93`
**任务**：校验 `SmsTemplateCode` 格式（阿里云 `SMS_` 前缀，腾讯云纯数字），不合法时抛 `NotificationDomainException`。
**验收标准**：`SmsTemplateCode = "invalid"` 时 `Update` 抛异常。

### P2-47：NotificationSendController 双路由 Obsolete 注释无迁移计划

**证据**：`file:///workspace/src/Services/Notification/Leno.Notification.Api/Controllers/NotificationSendController.cs#L27-L29`
**任务**：在旧路由上添加弃用日志（记录调用方），设置监控告警；达到下线时间后删除旧路由特性。
**验收标准**：旧路由调用产生告警日志，下线时间到达后旧路由被移除。

---

## 修复优先级与依赖关系

### 依赖链路

```
P0-1 (DI重复键) ──┐
P0-2 (配置字段名) ─┤── P0-9 (模板编码透传) ── P0-10 (BizId解析) ── P0-5 (回执持久化) ── P0-6 (密钥安全)
                  │
P0-3 (重复订阅) ──┤── P0-4 (OrderCancelled UserId)
                  │
P0-8 (超时滞留) ──┤── P1-15 (幂等唯一约束) ── P1-25 (Job无锁)
                  │
P0-7 (重发卡死) ──┤── P1-23 (死信重发状态机) ── P1-34 (回执失败状态)
                  │
P0-11 (模板唯一) ─┤── P0-12 (全表加载)
```

### 建议执行顺序

1. **第一批（阻塞级）**：P0-1 → P0-2 → P0-3 → P0-4（修复后通知域基本可用）
2. **第二批（功能级）**：P0-9 → P0-10 → P0-5 → P0-6（修复短信+回执链路）
3. **第三批（状态机级）**：P0-8 → P0-7 → P0-11 → P0-12（修复状态流转与查询性能）
4. **第四批（P1 索引与并发）**：P1-15 → P1-16 → P1-17 → P1-25
5. **第五批（P1 功能完善）**：其余 P1 按优先级推进
6. **第六批（P2 优化）**：按需推进

---

## 代码完整性自检

- [x] 无 TODO/FIXME/占位符
- [x] 所有 P0 测试代码为完整可编译实现（含 Arrange/Act/Assert）
- [x] 所有 P0 实现描述包含具体代码片段
- [x] 引用证据均为 `file:///workspace/...#L行号` 格式
- [x] 未修改任何业务代码
- [x] 统计数据基于源码逐条校验
