using System.Globalization;
using System.Reflection;
using System.Resources;
using Leno.Infrastructure.Abstractions.Localization;

namespace Leno.Infrastructure.Localization;

/// <summary>
/// 基于 <see cref="ResourceManager"/> 的本地化器（真实实现，DG-8 决策门通过后启用）。
/// <para>
/// 从 <c>.resx</c> 嵌入资源按当前 UI 文化查询本地化字符串，未命中时回退到中性资源，
/// 再未命中时返回 key 本身（保证调用方始终拿到非空字符串）。
/// </para>
/// <para>
/// 当前阶段 DI 容器默认注册 <see cref="NullStringLocalizer"/>，本类型不参与默认行为。
/// 业务方确认海外扩展计划后，通过 <see cref="LocalizationExtensions.AddResourceManagerLocalization"/> 切换启用。
/// </para>
/// </summary>
public sealed class ResourceManagerStringLocalizer : IStringLocalizer
{
    private readonly ResourceManager _resourceManager;
    private readonly CultureInfo _culture;

    /// <summary>
    /// 构造本地化器。
    /// </summary>
    /// <param name="resourceManager">已配置资源基名与程序集的 <see cref="ResourceManager"/>。</param>
    /// <param name="culture">
    /// 查询使用的文化。为 <c>null</c> 时使用 <see cref="CultureInfo.CurrentUICulture"/>，
    /// 支持按请求 <c>Accept-Language</c> 头动态切换（DG-8 通过后由 CultureMiddleware 注入）。
    /// </param>
    public ResourceManagerStringLocalizer(ResourceManager resourceManager, CultureInfo? culture = null)
    {
        ArgumentNullException.ThrowIfNull(resourceManager);
        _resourceManager = resourceManager;
        _culture = culture ?? CultureInfo.CurrentUICulture;
    }

    /// <inheritdoc />
    public string this[string key]
    {
        get
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            return _resourceManager.GetString(key, _culture) ?? key;
        }
    }

    /// <inheritdoc />
    public string this[string key, params object[] arguments]
    {
        get
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            var template = _resourceManager.GetString(key, _culture) ?? key;
            return arguments is null || arguments.Length == 0
                ? template
                : string.Format(_culture, template, arguments);
        }
    }
}

/// <summary>
/// 基于 <see cref="ResourceManager"/> 的本地化器工厂（真实实现，DG-8 决策门通过后启用）。
/// 按 <c>baseName</c> 在指定程序集中定位 <c>.resx</c> 嵌入资源并创建 <see cref="ResourceManagerStringLocalizer"/>。
/// </summary>
public sealed class ResourceManagerStringLocalizerFactory : IStringLocalizerFactory
{
    private readonly Assembly _resourceAssembly;
    private readonly string _resourceNamespace;
    private readonly CultureInfo _culture;

    /// <summary>
    /// 构造工厂。
    /// </summary>
    /// <param name="resourceAssembly">承载 <c>.resx</c> 嵌入资源的程序集。</param>
    /// <param name="resourceNamespace">资源所在命名空间（如 "Leno.SharedContracts.Localization.Resources"）。</param>
    /// <param name="culture">查询使用的文化，为 <c>null</c> 时使用 <see cref="CultureInfo.CurrentUICulture"/>。</param>
    public ResourceManagerStringLocalizerFactory(
        Assembly resourceAssembly,
        string resourceNamespace,
        CultureInfo? culture = null)
    {
        ArgumentNullException.ThrowIfNull(resourceAssembly);
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceNamespace);
        _resourceAssembly = resourceAssembly;
        _resourceNamespace = resourceNamespace;
        _culture = culture ?? CultureInfo.CurrentUICulture;
    }

    /// <inheritdoc />
    public IStringLocalizer Create(string baseName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseName);
        var fullBaseName = $"{_resourceNamespace}.{baseName}";
        var resourceManager = new ResourceManager(fullBaseName, _resourceAssembly);
        return new ResourceManagerStringLocalizer(resourceManager, _culture);
    }
}
