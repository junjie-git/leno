using Leno.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Leno.Infrastructure.Tests.Configuration;

/// <summary>
/// T19 验证：ConsulReloadableConfigurationProvider.SetValue 触发 IOptionsMonitor&lt;TestOptions&gt; 重载。
/// 使用中性键 <c>Test:ReloadableKey</c>（原灰度开关键已随双轨下线 B2 删除，机制与具体键名解耦）。
/// </summary>
public class ConsulReloadableConfigurationProviderTests
{
    /// <summary>测试专用绑定目标：验证 provider 触发 IOptionsMonitor 重载的最小 Options 形状。</summary>
    private sealed class TestOptions
    {
        public string? ReloadableKey { get; set; }
    }

    [Fact]
    public void SetValue_TriggersOptionsMonitorReload_CurrentValueUpdates()
    {
        // Arrange — 构造配置链：appsettings（before）+ Consul provider（链尾覆盖）
        var consulProvider = new ConsulReloadableConfigurationProvider();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string?>("Test:ReloadableKey", "before")
            })
            .Add(new ConsulReloadableConfigurationSource(consulProvider))
            .Build();

        var services = new ServiceCollection();
        services.Configure<TestOptions>(config.GetSection("Test"));
        using var sp = services.BuildServiceProvider();
        var monitor = sp.GetRequiredService<IOptionsMonitor<TestOptions>>();

        // 初始值应为 in-memory 提供的 before
        monitor.CurrentValue.ReloadableKey.Should().Be("before");

        // Act — 模拟 Consul KV 变更
        consulProvider.SetValue("Test:ReloadableKey", "after");

        // Assert — IOptionsMonitor 应感知重载，CurrentValue 更新为 after
        monitor.CurrentValue.ReloadableKey.Should().Be("after");
    }

    [Fact]
    public void SetValue_TriggersOnChangeCallback()
    {
        // Arrange
        var consulProvider = new ConsulReloadableConfigurationProvider();
        var config = new ConfigurationBuilder()
            .Add(new ConsulReloadableConfigurationSource(consulProvider))
            .Build();

        var services = new ServiceCollection();
        services.Configure<TestOptions>(config.GetSection("Test"));
        using var sp = services.BuildServiceProvider();
        var monitor = sp.GetRequiredService<IOptionsMonitor<TestOptions>>();

        var callbackHit = false;
        monitor.OnChange(_ => callbackHit = true);

        // Act
        consulProvider.SetValue("Test:ReloadableKey", "after");

        // Assert — OnChange 回调应被触发（IOptionsMonitor 监听到 ReloadToken 变化）
        // 给OnChange回调一点时间执行（它是异步的）
        Thread.Sleep(100);
        callbackHit.Should().BeTrue();
    }

    [Fact]
    public void SetValue_OverridesEarlierProviderValue()
    {
        // Arrange — in-memory 提供初始值 before，Consul provider（链尾）覆盖为 after
        var consulProvider = new ConsulReloadableConfigurationProvider();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string?>("Test:ReloadableKey", "before")
            })
            .Add(new ConsulReloadableConfigurationSource(consulProvider))
            .Build();

        // 配置读取应返回 Consul provider 的值（链尾优先）
        // 但 Consul provider 初始无值，所以仍返回 in-memory 的 before
        config["Test:ReloadableKey"].Should().Be("before");

        // Act
        consulProvider.SetValue("Test:ReloadableKey", "after");

        // Assert — Consul provider 覆盖 in-memory
        config["Test:ReloadableKey"].Should().Be("after");
    }

    [Fact]
    public void SetValue_NullValue_RemovesKey()
    {
        // Arrange
        var consulProvider = new ConsulReloadableConfigurationProvider();
        consulProvider.SetValue("Test:ReloadableKey", "after");

        var config = new ConfigurationBuilder()
            .Add(new ConsulReloadableConfigurationSource(consulProvider))
            .Build();

        config["Test:ReloadableKey"].Should().Be("after");

        // Act — null 移除 key
        consulProvider.SetValue("Test:ReloadableKey", null);

        // Assert
        config["Test:ReloadableKey"].Should().BeNull();
    }

    [Fact]
    public void SetValue_EmptyKey_Throws()
    {
        var consulProvider = new ConsulReloadableConfigurationProvider();
        var act = () => consulProvider.SetValue("", "value");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TrySet_ReturnsTrueAndTriggersReload()
    {
        // Arrange — 通过 IConfiguration 索引器写入也应触发 OnReload
        var consulProvider = new ConsulReloadableConfigurationProvider();
        var config = new ConfigurationBuilder()
            .Add(new ConsulReloadableConfigurationSource(consulProvider))
            .Build();

        var services = new ServiceCollection();
        services.Configure<TestOptions>(config.GetSection("Test"));
        using var sp = services.BuildServiceProvider();
        var monitor = sp.GetRequiredService<IOptionsMonitor<TestOptions>>();

        monitor.CurrentValue.ReloadableKey.Should().BeNull();

        // Act — 通过 IConfiguration 索引器写入（走 provider 重写的 Set → SetValue → OnReload）
        config["Test:ReloadableKey"] = "after";

        // Assert — TrySet 重写后触发 OnReload，IOptionsMonitor 重载
        monitor.CurrentValue.ReloadableKey.Should().Be("after");
    }
}
