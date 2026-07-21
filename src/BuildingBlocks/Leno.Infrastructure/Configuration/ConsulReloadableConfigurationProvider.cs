using Microsoft.Extensions.Configuration;

namespace Leno.Infrastructure.Configuration;

/// <summary>
/// 可重载的 Consul 配置提供者（T19 修复）。
/// <para>
/// 背景：直接 <c>_configuration["key"] = value</c> 写入 <see cref="IConfiguration"/> 时，
/// 若链中无可写 provider（JSON/Env 均只读），值被静默丢弃；即使存在 MemoryConfigurationProvider，
/// 也不会触发 <see cref="IOptionsMonitor{T}"/> 重载。导致 <c>IOptionsMonitor&lt;AntiCorruptionOptions&gt;</c>
/// 无法感知 Consul KV 热更新。
/// </para>
/// <para>
/// 修复：自定义 <see cref="IConfigurationProvider"/>，<see cref="SetValue"/> 更新 Data 字典后
/// 调用 <see cref="ConfigurationProvider.OnReload"/> 触发 ReloadToken，
/// IOptionsMonitor 监听到 token 变化后重新绑定 <c>AntiCorruptionOptions</c>。
/// </para>
/// <para>
/// 该 provider 在 <see cref="WebApplicationExtensions.AddLenoApi"/> 中作为最高优先级（链尾）
/// 加入配置链，确保 Consul KV 值覆盖 appsettings.json 等静态源。
/// </para>
/// </summary>
public sealed class ConsulReloadableConfigurationProvider : ConfigurationProvider
{
    /// <summary>
    /// 更新配置值并触发 IOptionsMonitor 重载。
    /// </summary>
    /// <param name="key">配置键（如 <c>AntiCorruption:UseGrpc</c>）。</param>
    /// <param name="value">配置值。null 表示移除该键。</param>
    public void SetValue(string key, string? value)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        if (value is null)
        {
            Data.Remove(key);
        }
        else
        {
            Data[key] = value;
        }

        // 触发 ReloadToken，IOptionsMonitor<T> 收到通知后重新从所有 provider 读取 CurrentValue
        OnReload();
    }

    /// <summary>
    /// 重写 TrySet 使其支持通过 IConfiguration 索引器写入并触发重载。
    /// 保留默认 Set 行为的同时增加 OnReload 通知。
    /// </summary>
    public override bool TrySet(string key, string? value)
    {
        SetValue(key, value);
        return true;
    }
}

/// <summary>
/// <see cref="ConsulReloadableConfigurationProvider"/> 的配置源。
/// 共享同一个 provider 实例，使 DI 容器与配置链引用同一可变状态。
/// </summary>
public sealed class ConsulReloadableConfigurationSource : IConfigurationSource
{
    private readonly ConsulReloadableConfigurationProvider _provider;

    public ConsulReloadableConfigurationSource(ConsulReloadableConfigurationProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    /// <summary>
    /// 返回共享的 provider 实例（不每次新建），使配置链与 DI 引用同一可变状态。
    /// </summary>
    public IConfigurationProvider Build(IConfigurationBuilder builder) => _provider;
}
