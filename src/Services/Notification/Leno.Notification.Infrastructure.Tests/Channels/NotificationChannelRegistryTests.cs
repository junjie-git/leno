using Leno.Notification.Domain.Channels;
using Leno.Notification.Domain.Services;
using Leno.Notification.Domain.ValueObjects;
using Leno.Notification.Infrastructure.Channels;
using Microsoft.Extensions.Logging;

namespace Leno.Notification.Infrastructure.Tests.Channels;

/// <summary>
/// 通知渠道注册表单元测试，验证 GetAllChannels / GetChannel / IsRegistered / GetChannelsByCapability。
/// </summary>
public class NotificationChannelRegistryTests
{
    private static NotificationChannelMetadata BuildMetadata(
        ChannelKey key,
        string displayName,
        int priority,
        bool isEnabled = true,
        bool requiresRateLimit = false,
        bool supportsAsyncReceipt = false,
        bool isIdempotent = false,
        bool supportsTemplate = true,
        TimeSpan? timeout = null)
    {
        return new NotificationChannelMetadata(
            key,
            displayName,
            new NotificationChannelCapabilities(
                requiresRateLimit, supportsAsyncReceipt, isIdempotent, supportsTemplate, timeout),
            isEnabled,
            priority);
    }

    /// <summary>
    /// 测试桩渠道实现，仅返回指定 Metadata，不依赖外部资源（Redis / SMTP / HTTP）。
    /// </summary>
    private sealed class StubChannel : INotificationChannel
    {
        public StubChannel(NotificationChannelMetadata metadata)
        {
            Metadata = metadata;
        }

        public NotificationChannel Channel { get; } = NotificationChannel.InApp;

        public ChannelKey ChannelKey => Metadata.Key;

        public NotificationChannelMetadata Metadata { get; }

        public Task<ChannelSendResult> SendAsync(ChannelSendRequest request, CancellationToken ct = default)
        {
            return Task.FromResult(new ChannelSendResult(true, null, null, "stub-id"));
        }
    }

    private static StubChannel Stub(NotificationChannelMetadata metadata) => new(metadata);

    #region 构造与 GetAllChannels

    [Fact]
    public void Constructor_NullChannels_ShouldThrowArgumentNullException()
    {
        var act = () => new NotificationChannelRegistry(
            null!,
            new Mock<ILogger<NotificationChannelRegistry>>().Object);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullLogger_ShouldThrowArgumentNullException()
    {
        var act = () => new NotificationChannelRegistry(
            Array.Empty<INotificationChannel>(),
            null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_EmptyChannels_ShouldProduceEmptyRegistry()
    {
        var registry = new NotificationChannelRegistry(
            Array.Empty<INotificationChannel>(),
            new Mock<ILogger<NotificationChannelRegistry>>().Object);

        registry.GetAllChannels().Should().BeEmpty();
    }

    [Fact]
    public void GetAllChannels_ShouldReturnAllRegisteredChannels()
    {
        var sms = Stub(BuildMetadata(ChannelKey.Sms, "短信", priority: 10));
        var email = Stub(BuildMetadata(ChannelKey.Email, "邮件", priority: 20));
        var inApp = Stub(BuildMetadata(ChannelKey.InApp, "站内信", priority: 30));

        var registry = new NotificationChannelRegistry(
            new INotificationChannel[] { sms, email, inApp },
            new Mock<ILogger<NotificationChannelRegistry>>().Object);

        var all = registry.GetAllChannels();
        all.Should().HaveCount(3);
        all.Select(m => (string)m.Key).Should().BeEquivalentTo(new[] { "Sms", "Email", "InApp" });
    }

    [Fact]
    public void GetAllChannels_ShouldReturnChannelsOrderedByPriorityAscending()
    {
        // 按 Priority 降序注册，但 GetAllChannels 应按升序返回
        var inApp = Stub(BuildMetadata(ChannelKey.InApp, "站内信", priority: 30));
        var email = Stub(BuildMetadata(ChannelKey.Email, "邮件", priority: 20));
        var sms = Stub(BuildMetadata(ChannelKey.Sms, "短信", priority: 10));

        var registry = new NotificationChannelRegistry(
            new INotificationChannel[] { inApp, email, sms },
            new Mock<ILogger<NotificationChannelRegistry>>().Object);

        var all = registry.GetAllChannels();
        all.Should().HaveCount(3);
        all[0].Key.Should().Be(ChannelKey.Sms);
        all[1].Key.Should().Be(ChannelKey.Email);
        all[2].Key.Should().Be(ChannelKey.InApp);
    }

    [Fact]
    public void GetAllChannels_ShouldReturnSnapshotImmutableByReRegistration()
    {
        // 注册一次后多次 GetAllChannels 调用应返回同一快照
        var sms = Stub(BuildMetadata(ChannelKey.Sms, "短信", priority: 10));

        var registry = new NotificationChannelRegistry(
            new INotificationChannel[] { sms },
            new Mock<ILogger<NotificationChannelRegistry>>().Object);

        var first = registry.GetAllChannels();
        var second = registry.GetAllChannels();

        first.Should().BeSameAs(second);
    }

    #endregion

    #region GetChannel

    [Fact]
    public void GetChannel_RegisteredKey_ShouldReturnMetadata()
    {
        var sms = Stub(BuildMetadata(ChannelKey.Sms, "短信", priority: 10, requiresRateLimit: true));
        var registry = new NotificationChannelRegistry(
            new INotificationChannel[] { sms },
            new Mock<ILogger<NotificationChannelRegistry>>().Object);

        var metadata = registry.GetChannel(ChannelKey.Sms);

        metadata.Should().NotBeNull();
        metadata!.Key.Should().Be(ChannelKey.Sms);
        metadata.DisplayName.Should().Be("短信");
        metadata.Capabilities.RequiresRateLimit.Should().BeTrue();
    }

    [Fact]
    public void GetChannel_UnregisteredKey_ShouldReturnNull()
    {
        var sms = Stub(BuildMetadata(ChannelKey.Sms, "短信", priority: 10));
        var registry = new NotificationChannelRegistry(
            new INotificationChannel[] { sms },
            new Mock<ILogger<NotificationChannelRegistry>>().Object);

        var metadata = registry.GetChannel(ChannelKey.Push);

        metadata.Should().BeNull();
    }

    [Fact]
    public void GetChannel_ByStringValue_ShouldMatchChannelKey()
    {
        // 验证 ChannelKey 字符串值相等性可正确查找
        var push = Stub(BuildMetadata(ChannelKey.Push, "推送", priority: 40));
        var registry = new NotificationChannelRegistry(
            new INotificationChannel[] { push },
            new Mock<ILogger<NotificationChannelRegistry>>().Object);

        var metadata = registry.GetChannel(new ChannelKey("Push"));

        metadata.Should().NotBeNull();
        metadata!.Key.Should().Be(ChannelKey.Push);
    }

    #endregion

    #region IsRegistered

    [Fact]
    public void IsRegistered_RegisteredKey_ShouldReturnTrue()
    {
        var email = Stub(BuildMetadata(ChannelKey.Email, "邮件", priority: 20));
        var registry = new NotificationChannelRegistry(
            new INotificationChannel[] { email },
            new Mock<ILogger<NotificationChannelRegistry>>().Object);

        registry.IsRegistered(ChannelKey.Email).Should().BeTrue();
    }

    [Fact]
    public void IsRegistered_UnregisteredKey_ShouldReturnFalse()
    {
        var email = Stub(BuildMetadata(ChannelKey.Email, "邮件", priority: 20));
        var registry = new NotificationChannelRegistry(
            new INotificationChannel[] { email },
            new Mock<ILogger<NotificationChannelRegistry>>().Object);

        registry.IsRegistered(ChannelKey.Sms).Should().BeFalse();
        registry.IsRegistered(ChannelKey.InApp).Should().BeFalse();
        registry.IsRegistered(ChannelKey.Push).Should().BeFalse();
    }

    [Fact]
    public void IsRegistered_EmptyRegistry_ShouldReturnFalse()
    {
        var registry = new NotificationChannelRegistry(
            Array.Empty<INotificationChannel>(),
            new Mock<ILogger<NotificationChannelRegistry>>().Object);

        registry.IsRegistered(ChannelKey.Sms).Should().BeFalse();
    }

    #endregion

    #region GetChannelsByCapability

    [Fact]
    public void GetChannelsByCapability_RequiresRateLimitTrue_ShouldFilterSmsOnly()
    {
        var sms = Stub(BuildMetadata(ChannelKey.Sms, "短信", priority: 10, requiresRateLimit: true));
        var email = Stub(BuildMetadata(ChannelKey.Email, "邮件", priority: 20, requiresRateLimit: false));
        var inApp = Stub(BuildMetadata(ChannelKey.InApp, "站内信", priority: 30, requiresRateLimit: false));

        var registry = new NotificationChannelRegistry(
            new INotificationChannel[] { sms, email, inApp },
            new Mock<ILogger<NotificationChannelRegistry>>().Object);

        var filtered = registry.GetChannelsByCapability(c => c.RequiresRateLimit).ToList();

        filtered.Should().HaveCount(1);
        filtered[0].Key.Should().Be(ChannelKey.Sms);
    }

    [Fact]
    public void GetChannelsByCapability_SupportsAsyncReceiptTrue_ShouldFilterSmsAndEmail()
    {
        var sms = Stub(BuildMetadata(ChannelKey.Sms, "短信", priority: 10, supportsAsyncReceipt: true));
        var email = Stub(BuildMetadata(ChannelKey.Email, "邮件", priority: 20, supportsAsyncReceipt: true));
        var inApp = Stub(BuildMetadata(ChannelKey.InApp, "站内信", priority: 30, supportsAsyncReceipt: false));

        var registry = new NotificationChannelRegistry(
            new INotificationChannel[] { sms, email, inApp },
            new Mock<ILogger<NotificationChannelRegistry>>().Object);

        var filtered = registry.GetChannelsByCapability(c => c.SupportsAsyncReceipt).ToList();

        filtered.Should().HaveCount(2);
        filtered.Select(m => (string)m.Key).Should().BeEquivalentTo(new[] { "Sms", "Email" });
    }

    [Fact]
    public void GetChannelsByCapability_IsIdempotentTrue_ShouldFilterInAppOnly()
    {
        var sms = Stub(BuildMetadata(ChannelKey.Sms, "短信", priority: 10, isIdempotent: false));
        var email = Stub(BuildMetadata(ChannelKey.Email, "邮件", priority: 20, isIdempotent: false));
        var inApp = Stub(BuildMetadata(ChannelKey.InApp, "站内信", priority: 30, isIdempotent: true));

        var registry = new NotificationChannelRegistry(
            new INotificationChannel[] { sms, email, inApp },
            new Mock<ILogger<NotificationChannelRegistry>>().Object);

        var filtered = registry.GetChannelsByCapability(c => c.IsIdempotent).ToList();

        filtered.Should().HaveCount(1);
        filtered[0].Key.Should().Be(ChannelKey.InApp);
    }

    [Fact]
    public void GetChannelsByCapability_NoneMatch_ShouldReturnEmpty()
    {
        var sms = Stub(BuildMetadata(ChannelKey.Sms, "短信", priority: 10, requiresRateLimit: false));

        var registry = new NotificationChannelRegistry(
            new INotificationChannel[] { sms },
            new Mock<ILogger<NotificationChannelRegistry>>().Object);

        var filtered = registry.GetChannelsByCapability(c => c.RequiresRateLimit).ToList();

        filtered.Should().BeEmpty();
    }

    [Fact]
    public void GetChannelsByCapability_NullPredicate_ShouldThrowArgumentNullException()
    {
        var registry = new NotificationChannelRegistry(
            Array.Empty<INotificationChannel>(),
            new Mock<ILogger<NotificationChannelRegistry>>().Object);

        var act = () => registry.GetChannelsByCapability(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetChannelsByCapability_ComplexPredicate_ShouldFilterByTimeout()
    {
        var sms = Stub(BuildMetadata(ChannelKey.Sms, "短信", priority: 10, timeout: TimeSpan.FromSeconds(30)));
        var email = Stub(BuildMetadata(ChannelKey.Email, "邮件", priority: 20, timeout: TimeSpan.FromSeconds(60)));
        var inApp = Stub(BuildMetadata(ChannelKey.InApp, "站内信", priority: 30, timeout: null));

        var registry = new NotificationChannelRegistry(
            new INotificationChannel[] { sms, email, inApp },
            new Mock<ILogger<NotificationChannelRegistry>>().Object);

        // 过滤 timeout 不为 null 且 <= 30s 的渠道
        var filtered = registry.GetChannelsByCapability(c => c.Timeout.HasValue && c.Timeout.Value <= TimeSpan.FromSeconds(30)).ToList();

        filtered.Should().HaveCount(1);
        filtered[0].Key.Should().Be(ChannelKey.Sms);
    }

    #endregion

    #region 重复 Key 处理

    [Fact]
    public void Constructor_DuplicateKeys_ShouldKeepFirstAndIgnoreSubsequent()
    {
        var first = Stub(BuildMetadata(ChannelKey.Sms, "短信（主）", priority: 10));
        var duplicate = Stub(BuildMetadata(ChannelKey.Sms, "短信（重复）", priority: 5));

        var registry = new NotificationChannelRegistry(
            new INotificationChannel[] { first, duplicate },
            new Mock<ILogger<NotificationChannelRegistry>>().Object);

        var all = registry.GetAllChannels();
        all.Should().HaveCount(1);
        all[0].DisplayName.Should().Be("短信（主）");
    }

    #endregion

    #region 真实渠道集成验证（PushChannel 注册零侵入）

    [Fact]
    public void Registry_WithRealPushChannel_ShouldDiscoverPushChannelAutomatically()
    {
        // 验证"新增渠道实现 IChannel + DI 注册即可被注册表自动发现"：
        // PushChannel 实现自描述 Metadata，注册表无需修改即可发现。
        var pushChannel = new PushChannel(new Mock<ILogger<PushChannel>>().Object);

        var registry = new NotificationChannelRegistry(
            new INotificationChannel[] { pushChannel },
            new Mock<ILogger<NotificationChannelRegistry>>().Object);

        registry.IsRegistered(ChannelKey.Push).Should().BeTrue();
        var metadata = registry.GetChannel(ChannelKey.Push);
        metadata.Should().NotBeNull();
        metadata!.Key.Should().Be(ChannelKey.Push);
        metadata.DisplayName.Should().Be("推送");
        metadata.Priority.Should().Be(40);
        metadata.Capabilities.SupportsAsyncReceipt.Should().BeTrue();
        metadata.Capabilities.Timeout.Should().Be(TimeSpan.FromSeconds(15));
    }

    [Fact]
    public async Task PushChannel_SendAsync_ShouldReturnSucceededResult()
    {
        // 验证 PushChannel mock 完整实现，非空方法
        var pushChannel = new PushChannel(new Mock<ILogger<PushChannel>>().Object);
        var recipient = Recipient.Create(Guid.NewGuid(), "test@example.com", "13800138000");
        var request = new ChannelSendRequest(
            NotificationChannel.InApp, recipient, "测试推送", "推送内容", "idem-push-1");

        var result = await pushChannel.SendAsync(request, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.ErrorCode.Should().BeNull();
        result.ErrorMessage.Should().BeNull();
        result.ChannelMessageId.Should().NotBeNullOrEmpty();
        result.ChannelMessageId.Should().StartWith("push-");
    }

    #endregion
}
