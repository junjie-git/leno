using Leno.Infrastructure.Abstractions.Localization;

namespace Leno.Infrastructure.Localization;

/// <summary>
/// 空本地化器（默认实现，预留扩展位）。
/// 原样返回 key，不执行任何本地化翻译，保证现有错误消息行为零变更。
/// <para>
/// DG-8 决策门通过后，DI 容器切换为 <see cref="ResourceManagerStringLocalizer"/> 启用多语言资源查询。
/// </para>
/// </summary>
public sealed class NullStringLocalizer : IStringLocalizer
{
    /// <summary>单例实例（无状态，可安全共享）。</summary>
    public static readonly NullStringLocalizer Instance = new();

    private NullStringLocalizer() { }

    /// <inheritdoc />
    public string this[string key]
    {
        get
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            return key;
        }
    }

    /// <inheritdoc />
    public string this[string key, params object[] arguments]
    {
        get
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            return arguments is null || arguments.Length == 0
                ? key
                : string.Format(key, arguments);
        }
    }
}

/// <summary>
/// 空本地化器工厂（默认实现，预留扩展位）。
/// 对任意 <c>baseName</c> 均返回 <see cref="NullStringLocalizer"/> 单例。
/// </summary>
public sealed class NullStringLocalizerFactory : IStringLocalizerFactory
{
    /// <summary>单例实例（无状态，可安全共享）。</summary>
    public static readonly NullStringLocalizerFactory Instance = new();

    private NullStringLocalizerFactory() { }

    /// <inheritdoc />
    public IStringLocalizer Create(string baseName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseName);
        return NullStringLocalizer.Instance;
    }
}
