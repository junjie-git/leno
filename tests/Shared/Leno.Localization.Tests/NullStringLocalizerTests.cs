using Leno.Infrastructure.Abstractions.Localization;
using Leno.Infrastructure.Localization;

namespace Leno.Localization.Tests;

/// <summary>
/// NullStringLocalizer 单元测试（国际化预留扩展位默认实现）。
/// 验证空本地化器原样返回 key，不执行任何翻译，保证现有错误消息行为零变更。
/// </summary>
public sealed class NullStringLocalizerTests
{
    /// <summary>
    /// 单例实例应可用且无状态，多次引用应返回同一实例。
    /// </summary>
    [Fact]
    public void Instance_ShouldBeSingleton()
    {
        var instance1 = NullStringLocalizer.Instance;
        var instance2 = NullStringLocalizer.Instance;

        instance1.Should().BeSameAs(instance2);
    }

    /// <summary>
    /// 单索引器应原样返回 key（不翻译），保证默认行为不变。
    /// </summary>
    [Fact]
    public void Indexer_SingleKey_ShouldReturnKeyAsIs()
    {
        var localizer = NullStringLocalizer.Instance;
        const string key = "cart_not_found";

        var result = localizer[key];

        result.Should().Be(key);
    }

    /// <summary>
    /// 单索引器对任意 key 均原样返回，包括未注册的 key。
    /// </summary>
    [Fact]
    public void Indexer_UnregisteredKey_ShouldReturnKeyAsIs()
    {
        var localizer = NullStringLocalizer.Instance;
        const string key = "non_existent_key_12345";

        var result = localizer[key];

        result.Should().Be(key);
    }

    /// <summary>
    /// 带参数索引器在无参数时应原样返回 key。
    /// </summary>
    [Fact]
    public void Indexer_WithEmptyArguments_ShouldReturnKeyAsIs()
    {
        var localizer = NullStringLocalizer.Instance;
        const string key = "order_timeout";

        var result = localizer[key];

        result.Should().Be(key);
    }

    /// <summary>
    /// 带参数索引器应使用 string.Format 格式化 key（key 作为模板）。
    /// </summary>
    [Fact]
    public void Indexer_WithArguments_ShouldFormatKeyWithArguments()
    {
        var localizer = NullStringLocalizer.Instance;
        const string key = "Order {0} timed out after {1} seconds";

        var result = localizer[key, "ORD-123", 30];

        result.Should().Be("Order ORD-123 timed out after 30 seconds");
    }

    /// <summary>
    /// 带参数索引器对 null 参数数组应原样返回 key。
    /// </summary>
    [Fact]
    public void Indexer_WithNullArguments_ShouldReturnKeyAsIs()
    {
        var localizer = NullStringLocalizer.Instance;
        const string key = "payment_failed";

#pragma warning disable CS8625 // 测试场景：模拟 null 参数数组
        var result = localizer[key, null!];
#pragma warning restore CS8625

        result.Should().Be(key);
    }

    /// <summary>
    /// 单索引器对空 key 应抛出 ArgumentException。
    /// </summary>
    [Fact]
    public void Indexer_EmptyKey_ShouldThrowArgumentException()
    {
        var localizer = NullStringLocalizer.Instance;

        var act = () => localizer[""];

        act.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// 单索引器对 null key 应抛出 ArgumentException。
    /// </summary>
    [Fact]
    public void Indexer_NullKey_ShouldThrowArgumentException()
    {
        var localizer = NullStringLocalizer.Instance;

#pragma warning disable CS8625 // 测试场景：模拟 null key
        var act = () => localizer[null!];
#pragma warning restore CS8625

        act.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// NullStringLocalizerFactory 对任意 baseName 应返回 NullStringLocalizer 单例。
    /// </summary>
    [Fact]
    public void Factory_Create_ShouldReturnNullStringLocalizerInstance()
    {
        var factory = NullStringLocalizerFactory.Instance;

        var localizer = factory.Create("ErrorMessages");

        localizer.Should().BeSameAs(NullStringLocalizer.Instance);
    }

    /// <summary>
    /// NullStringLocalizerFactory 单例实例应可用且无状态。
    /// </summary>
    [Fact]
    public void Factory_Instance_ShouldBeSingleton()
    {
        var instance1 = NullStringLocalizerFactory.Instance;
        var instance2 = NullStringLocalizerFactory.Instance;

        instance1.Should().BeSameAs(instance2);
    }
}
