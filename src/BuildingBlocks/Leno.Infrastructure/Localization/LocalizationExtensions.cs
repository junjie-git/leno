using System.Globalization;
using System.Reflection;
using Leno.Infrastructure.Abstractions.Localization;
using Microsoft.Extensions.DependencyInjection;

namespace Leno.Infrastructure.Localization;

/// <summary>
/// 本地化 DI 注册扩展（国际化预留扩展位）。
/// <para>
/// 默认调用 <see cref="AddLenoLocalization"/> 注册 <see cref="NullStringLocalizer"/>（空实现），
/// 保证现有错误消息行为零变更。DG-8 决策门通过后，业务方调用
/// <see cref="AddResourceManagerLocalization"/> 切换为 <see cref="ResourceManagerStringLocalizer"/>
/// 启用 <c>.resx</c> 多语言资源查询。
/// </para>
/// </summary>
public static class LocalizationExtensions
{
    /// <summary>
    /// 注册本地化服务（默认空实现）。
    /// <see cref="IStringLocalizer"/> 与 <see cref="IStringLocalizerFactory"/> 均解析为
    /// <see cref="NullStringLocalizer"/> / <see cref="NullStringLocalizerFactory"/>，原样返回 key。
    /// </summary>
    /// <param name="configure">可选的 <see cref="LocalizationOptions"/> 配置回调。</param>
    public static IServiceCollection AddLenoLocalization(
        this IServiceCollection services,
        Action<LocalizationOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new LocalizationOptions();
        configure?.Invoke(options);

        // 预留扩展位：默认空实现，不改变现有行为。
        services.AddSingleton(options);
        services.AddSingleton<IStringLocalizer>(NullStringLocalizer.Instance);
        services.AddSingleton<IStringLocalizerFactory>(NullStringLocalizerFactory.Instance);

        return services;
    }

    /// <summary>
    /// 注册基于 <see cref="ResourceManagerStringLocalizer"/> 的本地化服务（DG-8 决策门通过后启用）。
    /// <para>
    /// 当前阶段不调用此方法。业务方确认海外扩展计划后，在各 BC 的 <c>AddXxxInfrastructure</c> 中
    /// 调用此方法替代 <see cref="AddLenoLocalization"/>，并指定承载 <c>.resx</c> 资源的程序集与命名空间。
    /// </para>
    /// </summary>
    /// <param name="resourceAssembly">承载 <c>.resx</c> 嵌入资源的程序集。</param>
    /// <param name="resourceNamespace">资源所在命名空间（如 "Leno.SharedContracts.Localization.Resources"）。</param>
    /// <param name="configure">可选的 <see cref="LocalizationOptions"/> 配置回调。</param>
    /// <param name="culture">
    /// 查询使用的固定文化。为 <c>null</c> 时使用 <see cref="CultureInfo.CurrentUICulture"/>，
    /// 支持按请求 <c>Accept-Language</c> 头动态切换（DG-8 通过后由 CultureMiddleware 注入）。
    /// </param>
    public static IServiceCollection AddResourceManagerLocalization(
        this IServiceCollection services,
        Assembly resourceAssembly,
        string resourceNamespace,
        Action<LocalizationOptions>? configure = null,
        CultureInfo? culture = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(resourceAssembly);
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceNamespace);

        var options = new LocalizationOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<IStringLocalizerFactory>(_ =>
            new ResourceManagerStringLocalizerFactory(resourceAssembly, resourceNamespace, culture));
        services.AddSingleton<IStringLocalizer>(sp =>
        {
            var factory = sp.GetRequiredService<IStringLocalizerFactory>();
            return factory.Create("ErrorMessages");
        });

        return services;
    }
}
