using Leno.Infrastructure.AntiCorruption;
using Leno.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Leno.Infrastructure.Tests.Configuration;

/// <summary>
/// T19 验证：ConsulReloadableConfigurationProvider.SetValue 触发 IOptionsMonitor&lt;AntiCorruptionOptions&gt; 重载。
/// </summary>
public class ConsulReloadableConfigurationProviderTests
{
    [Fact]
    public void SetValue_TriggersOptionsMonitorReload_CurrentValueUpdates()
    {
        // Arrange — 构造配置链：appsettings（false）+ Consul provider（链尾覆盖）
        var consulProvider = new ConsulReloadableConfigurationProvider();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string?>("AntiCorruption:UseGrpc", "false")
            })
            .Add(new ConsulReloadableConfigurationSource(consulProvider))
            .Build();

        var services = new ServiceCollection();
        services.Configure<AntiCorruptionOptions>(config.GetSection("AntiCorruption"));
        using var sp = services.BuildServiceProvider();
        var monitor = sp.GetRequiredService<IOptionsMonitor<AntiCorruptionOptions>>();

        // 初始 UseGrpc 应为 false
        monitor.CurrentValue.UseGrpc.Should().BeFalse();

        // Act — 模拟 Consul KV 变更
        consulProvider.SetValue("AntiCorruption:UseGrpc", "true");

        // Assert — IOptionsMonitor 应感知重载，CurrentValue.UseGrpc 为 true
        monitor.CurrentValue.UseGrpc.Should().BeTrue();
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
        services.Configure<AntiCorruptionOptions>(config.GetSection("AntiCorruption"));
        using var sp = services.BuildServiceProvider();
        var monitor = sp.GetRequiredService<IOptionsMonitor<AntiCorruptionOptions>>();

        var callbackHit = false;
        monitor.OnChange(_ => callbackHit = true);

        // Act
        consulProvider.SetValue("AntiCorruption:UseGrpc", "true");

        // Assert — OnChange 回调应被触发（IOptionsMonitor 监听到 ReloadToken 变化）
        // 给OnChange回调一点时间执行（它是异步的）
        Thread.Sleep(100);
        callbackHit.Should().BeTrue();
    }

    [Fact]
    public void SetValue_OverridesEarlierProviderValue()
    {
        // Arrange — in-memory 提供初始值 false，Consul provider（链尾）覆盖为 true
        var consulProvider = new ConsulReloadableConfigurationProvider();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string?>("AntiCorruption:UseGrpc", "false")
            })
            .Add(new ConsulReloadableConfigurationSource(consulProvider))
            .Build();

        // 配置读取应返回 Consul provider 的值（链尾优先）
        // 但 Consul provider 初始无值，所以仍返回 in-memory 的 false
        config["AntiCorruption:UseGrpc"].Should().Be("false");

        // Act
        consulProvider.SetValue("AntiCorruption:UseGrpc", "true");

        // Assert — Consul provider 覆盖 in-memory
        config["AntiCorruption:UseGrpc"].Should().Be("true");
    }

    [Fact]
    public void SetValue_NullValue_RemovesKey()
    {
        // Arrange
        var consulProvider = new ConsulReloadableConfigurationProvider();
        consulProvider.SetValue("AntiCorruption:UseGrpc", "true");

        var config = new ConfigurationBuilder()
            .Add(new ConsulReloadableConfigurationSource(consulProvider))
            .Build();

        config["AntiCorruption:UseGrpc"].Should().Be("true");

        // Act — null 移除 key
        consulProvider.SetValue("AntiCorruption:UseGrpc", null);

        // Assert
        config["AntiCorruption:UseGrpc"].Should().BeNull();
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
        services.Configure<AntiCorruptionOptions>(config.GetSection("AntiCorruption"));
        using var sp = services.BuildServiceProvider();
        var monitor = sp.GetRequiredService<IOptionsMonitor<AntiCorruptionOptions>>();

        monitor.CurrentValue.UseGrpc.Should().BeFalse();

        // Act — 通过 IConfiguration 索引器写入（生产路径：ConsulConfigWatcher 的回退分支也用此方式）
        config["AntiCorruption:UseGrpc"] = "true";

        // Assert — TrySet 重写后触发 OnReload，IOptionsMonitor 重载
        monitor.CurrentValue.UseGrpc.Should().BeTrue();
    }
}
