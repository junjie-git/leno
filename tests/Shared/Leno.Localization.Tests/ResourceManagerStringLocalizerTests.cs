using System.Globalization;
using System.Resources;
using Leno.Infrastructure.Localization;
using Leno.SharedContracts.Localization;

namespace Leno.Localization.Tests;

/// <summary>
/// ResourceManagerStringLocalizer 单元测试（国际化预留扩展位真实实现）。
/// 覆盖 key 查找、缺省回退、参数格式化（spec 6.2.12）。
/// </summary>
public sealed class ResourceManagerStringLocalizerTests
{
    private static readonly ResourceManager ResourceManager =
        new("Leno.SharedContracts.Localization.Resources.ErrorMessages", typeof(ErrorCodeCatalog).Assembly);

    /// <summary>
    /// 构造本地化器时传入 null ResourceManager 应抛出 ArgumentNullException。
    /// </summary>
    [Fact]
    public void Constructor_NullResourceManager_ShouldThrowArgumentNullException()
    {
        var act = () => new ResourceManagerStringLocalizer(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// zh-CN 文化下查询已注册 key 应返回中文资源值。
    /// </summary>
    [Fact]
    public void Indexer_ZhCulture_RegisteredKey_ShouldReturnLocalizedValue()
    {
        var localizer = new ResourceManagerStringLocalizer(ResourceManager, new CultureInfo("zh-CN"));

        var result = localizer["cart_not_found"];

        result.Should().Be("购物车不存在");
    }

    /// <summary>
    /// en-US 文化下查询已注册 key 应返回英文资源值。
    /// </summary>
    [Fact]
    public void Indexer_EnCulture_RegisteredKey_ShouldReturnLocalizedValue()
    {
        var localizer = new ResourceManagerStringLocalizer(ResourceManager, new CultureInfo("en-US"));

        var result = localizer["cart_not_found"];

        result.Should().Be("Cart not found");
    }

    /// <summary>
    /// 未命中资源 key 时应回退到 key 本身（保证调用方始终拿到非空字符串）。
    /// </summary>
    [Fact]
    public void Indexer_UnregisteredKey_ShouldReturnKeyAsFallback()
    {
        var localizer = new ResourceManagerStringLocalizer(ResourceManager, new CultureInfo("zh-CN"));

        var result = localizer["non_existent_key_12345"];

        result.Should().Be("non_existent_key_12345");
    }

    /// <summary>
    /// 单索引器对空 key 应抛出 ArgumentException。
    /// </summary>
    [Fact]
    public void Indexer_EmptyKey_ShouldThrowArgumentException()
    {
        var localizer = new ResourceManagerStringLocalizer(ResourceManager, new CultureInfo("zh-CN"));

        var act = () => localizer[""];

        act.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// 带参数索引器应使用本地化模板进行格式化（zh-CN 文化）。
    /// </summary>
    [Fact]
    public void Indexer_WithArguments_ShouldFormatLocalizedTemplate()
    {
        var localizer = new ResourceManagerStringLocalizer(ResourceManager, new CultureInfo("zh-CN"));

        // 使用带 {0} 占位符的 fallback key 验证格式化（未命中资源时 key 作为模板）
        var result = localizer["Order {0} timeout", "ORD-123"];

        result.Should().Be("Order ORD-123 timeout");
    }

    /// <summary>
    /// 带参数索引器在无参数时应原样返回本地化值（非 null、空数组场景）。
    /// </summary>
    [Fact]
    public void Indexer_WithEmptyArguments_ShouldReturnLocalizedValue()
    {
        var localizer = new ResourceManagerStringLocalizer(ResourceManager, new CultureInfo("zh-CN"));

        var result = localizer["order_timeout"];

        result.Should().Be("订单已超时");
    }

    /// <summary>
    /// 带参数索引器对 null 参数数组应原样返回本地化值（非格式化）。
    /// </summary>
    [Fact]
    public void Indexer_WithNullArguments_ShouldReturnLocalizedValue()
    {
        var localizer = new ResourceManagerStringLocalizer(ResourceManager, new CultureInfo("zh-CN"));

#pragma warning disable CS8625 // 测试场景：模拟 null 参数数组
        var result = localizer["order_timeout", null!];
#pragma warning restore CS8625

        result.Should().Be("订单已超时");
    }

    /// <summary>
    /// 默认构造（不指定 culture）应使用 CurrentUICulture，切换线程文化后查询结果应相应变化。
    /// </summary>
    [Fact]
    public void Constructor_DefaultCulture_ShouldUseCurrentUICulture()
    {
        var originalCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("en-US");
            var localizer = new ResourceManagerStringLocalizer(ResourceManager);

            var result = localizer["order_timeout"];

            result.Should().Be("Order timed out");
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }

    /// <summary>
    /// zh-CN 文化下查询 notification_template_culture_invalid 应返回中文资源值（验证新增错误码资源）。
    /// </summary>
    [Fact]
    public void Indexer_ZhCulture_CultureInvalidKey_ShouldReturnLocalizedValue()
    {
        var localizer = new ResourceManagerStringLocalizer(ResourceManager, new CultureInfo("zh-CN"));

        var result = localizer["notification_template_culture_invalid"];

        result.Should().Be("通知模板文化无效");
    }

    /// <summary>
    /// en-US 文化下查询 notification_template_culture_invalid 应返回英文资源值（验证新增错误码资源）。
    /// </summary>
    [Fact]
    public void Indexer_EnCulture_CultureInvalidKey_ShouldReturnLocalizedValue()
    {
        var localizer = new ResourceManagerStringLocalizer(ResourceManager, new CultureInfo("en-US"));

        var result = localizer["notification_template_culture_invalid"];

        result.Should().Be("Notification template culture is invalid");
    }

    /// <summary>
    /// 查询 generic_error（回退 key）在 zh-CN 文化下应返回中文默认值。
    /// </summary>
    [Fact]
    public void Indexer_ZhCulture_FallbackKey_ShouldReturnLocalizedValue()
    {
        var localizer = new ResourceManagerStringLocalizer(ResourceManager, new CultureInfo("zh-CN"));

        var result = localizer["generic_error"];

        result.Should().Be("操作失败");
    }
}
