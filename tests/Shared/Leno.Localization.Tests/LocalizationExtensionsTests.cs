using Leno.Infrastructure.Abstractions.Localization;
using Leno.Infrastructure.Localization;
using Microsoft.Extensions.DependencyInjection;

namespace Leno.Localization.Tests;

/// <summary>
/// LocalizationExtensions DI 注册单元测试（国际化预留扩展位）。
/// 验证默认注册 NullStringLocalizer（空实现），不改变现有行为。
/// </summary>
public sealed class LocalizationExtensionsTests
{
    /// <summary>
    /// AddLenoLocalization 默认应注册 NullStringLocalizer 单例。
    /// </summary>
    [Fact]
    public void AddLenoLocalization_Default_ShouldRegisterNullStringLocalizer()
    {
        var services = new ServiceCollection();
        services.AddLenoLocalization();

        var sp = services.BuildServiceProvider();
        var localizer = sp.GetRequiredService<IStringLocalizer>();

        localizer.Should().BeSameAs(NullStringLocalizer.Instance);
    }

    /// <summary>
    /// AddLenoLocalization 默认应注册 NullStringLocalizerFactory 单例。
    /// </summary>
    [Fact]
    public void AddLenoLocalization_Default_ShouldRegisterNullStringLocalizerFactory()
    {
        var services = new ServiceCollection();
        services.AddLenoLocalization();

        var sp = services.BuildServiceProvider();
        var factory = sp.GetRequiredService<IStringLocalizerFactory>();

        factory.Should().BeSameAs(NullStringLocalizerFactory.Instance);
    }

    /// <summary>
    /// AddLenoLocalization 应注册 LocalizationOptions 单例，默认值正确。
    /// </summary>
    [Fact]
    public void AddLenoLocalization_Default_ShouldRegisterDefaultOptions()
    {
        var services = new ServiceCollection();
        services.AddLenoLocalization();

        var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<LocalizationOptions>();

        options.DefaultCulture.Should().Be("zh-CN");
        options.SupportedCultures.Should().BeEquivalentTo(new[] { "en-US", "zh-CN" });
    }

    /// <summary>
    /// AddLenoLocalization 配置回调应覆盖默认选项。
    /// </summary>
    [Fact]
    public void AddLenoLocalization_WithConfigure_ShouldOverrideOptions()
    {
        var services = new ServiceCollection();
        services.AddLenoLocalization(opts =>
        {
            opts.DefaultCulture = "en-US";
            opts.SupportedCultures = new[] { "en-US", "zh-CN", "ja-JP" };
        });

        var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<LocalizationOptions>();

        options.DefaultCulture.Should().Be("en-US");
        options.SupportedCultures.Should().BeEquivalentTo(new[] { "en-US", "zh-CN", "ja-JP" });
    }

    /// <summary>
    /// AddLenoLocalization 对 null services 应抛出 ArgumentNullException。
    /// </summary>
    [Fact]
    public void AddLenoLocalization_NullServices_ShouldThrowArgumentNullException()
    {
        IServiceCollection services = null!;

        var act = () => services.AddLenoLocalization();

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// AddLenoLocalization 注册的 IStringLocalizer 应为单例（多次解析返回同一实例）。
    /// </summary>
    [Fact]
    public void AddLenoLocalization_StringLocalizer_ShouldBeSingleton()
    {
        var services = new ServiceCollection();
        services.AddLenoLocalization();

        var sp = services.BuildServiceProvider();
        var localizer1 = sp.GetRequiredService<IStringLocalizer>();
        var localizer2 = sp.GetRequiredService<IStringLocalizer>();

        localizer1.Should().BeSameAs(localizer2);
    }

    /// <summary>
    /// AddLenoLocalization 注册的 IStringLocalizer 应原样返回 key（验证默认行为不变）。
    /// </summary>
    [Fact]
    public void AddLenoLocalization_DefaultLocalizer_ShouldReturnKeyAsIs()
    {
        var services = new ServiceCollection();
        services.AddLenoLocalization();

        var sp = services.BuildServiceProvider();
        var localizer = sp.GetRequiredService<IStringLocalizer>();

        localizer["cart_not_found"].Should().Be("cart_not_found");
    }

    /// <summary>
    /// AddResourceManagerLocalization 应注册 ResourceManagerStringLocalizer（真实实现）。
    /// </summary>
    [Fact]
    public void AddResourceManagerLocalization_ShouldRegisterResourceManagerStringLocalizer()
    {
        var services = new ServiceCollection();
        var resourceAssembly = typeof(Leno.SharedContracts.Localization.ErrorCodeCatalog).Assembly;

        services.AddResourceManagerLocalization(
            resourceAssembly,
            "Leno.SharedContracts.Localization.Resources");

        var sp = services.BuildServiceProvider();
        var localizer = sp.GetRequiredService<IStringLocalizer>();

        localizer.Should().BeOfType<ResourceManagerStringLocalizer>();
    }

    /// <summary>
    /// AddResourceManagerLocalization 注册的 IStringLocalizer 应能查询到实际资源值。
    /// </summary>
    [Fact]
    public void AddResourceManagerLocalization_ShouldResolveLocalizedValue()
    {
        var services = new ServiceCollection();
        var resourceAssembly = typeof(Leno.SharedContracts.Localization.ErrorCodeCatalog).Assembly;

        services.AddResourceManagerLocalization(
            resourceAssembly,
            "Leno.SharedContracts.Localization.Resources");

        var sp = services.BuildServiceProvider();
        var localizer = sp.GetRequiredService<IStringLocalizer>();

        // 默认使用 CurrentUICulture，此处仅验证能查询到非 key 值（即资源命中）
        // 不依赖特定文化，仅验证返回值不等于 key（资源命中）
        var result = localizer["cart_not_found"];
        result.Should().NotBe("cart_not_found");
    }

    /// <summary>
    /// AddResourceManagerLocalization 对 null services 应抛出 ArgumentNullException。
    /// </summary>
    [Fact]
    public void AddResourceManagerLocalization_NullServices_ShouldThrowArgumentNullException()
    {
        IServiceCollection services = null!;
        var resourceAssembly = typeof(Leno.SharedContracts.Localization.ErrorCodeCatalog).Assembly;

        var act = () => services.AddResourceManagerLocalization(
            resourceAssembly,
            "Leno.SharedContracts.Localization.Resources");

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// AddResourceManagerLocalization 对 null resourceAssembly 应抛出 ArgumentNullException。
    /// </summary>
    [Fact]
    public void AddResourceManagerLocalization_NullAssembly_ShouldThrowArgumentNullException()
    {
        var services = new ServiceCollection();

        var act = () => services.AddResourceManagerLocalization(
            null!,
            "Leno.SharedContracts.Localization.Resources");

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// AddResourceManagerLocalization 对空 resourceNamespace 应抛出 ArgumentException。
    /// </summary>
    [Fact]
    public void AddResourceManagerLocalization_EmptyNamespace_ShouldThrowArgumentException()
    {
        var services = new ServiceCollection();
        var resourceAssembly = typeof(Leno.SharedContracts.Localization.ErrorCodeCatalog).Assembly;

        var act = () => services.AddResourceManagerLocalization(resourceAssembly, "");

        act.Should().Throw<ArgumentException>();
    }
}
