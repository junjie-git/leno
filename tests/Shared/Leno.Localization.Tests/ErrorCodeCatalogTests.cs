using Leno.SharedContracts.Localization;

namespace Leno.Localization.Tests;

/// <summary>
/// ErrorCodeCatalog 单元测试（国际化预留扩展位）。
/// 验证错误码到本地化资源 key 的映射、回退行为。
/// </summary>
public sealed class ErrorCodeCatalogTests
{
    // ===== 已注册错误码映射测试 =====

    /// <summary>
    /// CART_NOT_FOUND 应映射到 cart_not_found 资源 key。
    /// </summary>
    [Fact]
    public void GetResourceKey_CartNotFound_ShouldReturnMappedKey()
    {
        var key = ErrorCodeCatalog.GetResourceKey("CART_NOT_FOUND");

        key.Should().Be("cart_not_found");
    }

    /// <summary>
    /// ORDER_TIMEOUT 应映射到 order_timeout 资源 key。
    /// </summary>
    [Fact]
    public void GetResourceKey_OrderTimeout_ShouldReturnMappedKey()
    {
        var key = ErrorCodeCatalog.GetResourceKey("ORDER_TIMEOUT");

        key.Should().Be("order_timeout");
    }

    /// <summary>
    /// PAYMENT_FAILED 应映射到 payment_failed 资源 key。
    /// </summary>
    [Fact]
    public void GetResourceKey_PaymentFailed_ShouldReturnMappedKey()
    {
        var key = ErrorCodeCatalog.GetResourceKey("PAYMENT_FAILED");

        key.Should().Be("payment_failed");
    }

    /// <summary>
    /// NOTIFICATION_TEMPLATE_CULTURE_INVALID 应映射到 notification_template_culture_invalid 资源 key（验证新增错误码）。
    /// </summary>
    [Fact]
    public void GetResourceKey_NotificationTemplateCultureInvalid_ShouldReturnMappedKey()
    {
        var key = ErrorCodeCatalog.GetResourceKey("NOTIFICATION_TEMPLATE_CULTURE_INVALID");

        key.Should().Be("notification_template_culture_invalid");
    }

    /// <summary>
    /// NOTIFICATION_TEMPLATE_ID_EMPTY 应映射到 notification_template_id_empty 资源 key。
    /// </summary>
    [Fact]
    public void GetResourceKey_NotificationTemplateIdEmpty_ShouldReturnMappedKey()
    {
        var key = ErrorCodeCatalog.GetResourceKey("NOTIFICATION_TEMPLATE_ID_EMPTY");

        key.Should().Be("notification_template_id_empty");
    }

    // ===== 回退行为测试 =====

    /// <summary>
    /// 未注册错误码应回退到 FallbackResourceKey（generic_error）。
    /// </summary>
    [Fact]
    public void GetResourceKey_UnregisteredCode_ShouldReturnFallbackKey()
    {
        var key = ErrorCodeCatalog.GetResourceKey("UNREGISTERED_ERROR_CODE_12345");

        key.Should().Be(ErrorCodeCatalog.FallbackResourceKey);
        key.Should().Be("generic_error");
    }

    /// <summary>
    /// null 错误码应回退到 FallbackResourceKey。
    /// </summary>
    [Fact]
    public void GetResourceKey_Null_ShouldReturnFallbackKey()
    {
        var key = ErrorCodeCatalog.GetResourceKey(null);

        key.Should().Be(ErrorCodeCatalog.FallbackResourceKey);
    }

    /// <summary>
    /// 空字符串错误码应回退到 FallbackResourceKey。
    /// </summary>
    [Fact]
    public void GetResourceKey_EmptyString_ShouldReturnFallbackKey()
    {
        var key = ErrorCodeCatalog.GetResourceKey("");

        key.Should().Be(ErrorCodeCatalog.FallbackResourceKey);
    }

    /// <summary>
    /// 纯空白字符串错误码应回退到 FallbackResourceKey。
    /// </summary>
    [Fact]
    public void GetResourceKey_Whitespace_ShouldReturnFallbackKey()
    {
        var key = ErrorCodeCatalog.GetResourceKey("   ");

        key.Should().Be(ErrorCodeCatalog.FallbackResourceKey);
    }

    // ===== IsRegistered 测试 =====

    /// <summary>
    /// IsRegistered 对已注册错误码应返回 true。
    /// </summary>
    [Fact]
    public void IsRegistered_RegisteredCode_ShouldReturnTrue()
    {
        ErrorCodeCatalog.IsRegistered("CART_NOT_FOUND").Should().BeTrue();
        ErrorCodeCatalog.IsRegistered("NOTIFICATION_TEMPLATE_CULTURE_INVALID").Should().BeTrue();
    }

    /// <summary>
    /// IsRegistered 对未注册错误码应返回 false。
    /// </summary>
    [Fact]
    public void IsRegistered_UnregisteredCode_ShouldReturnFalse()
    {
        ErrorCodeCatalog.IsRegistered("UNREGISTERED_ERROR_CODE_12345").Should().BeFalse();
    }

    /// <summary>
    /// IsRegistered 对 null 应返回 false。
    /// </summary>
    [Fact]
    public void IsRegistered_Null_ShouldReturnFalse()
    {
        ErrorCodeCatalog.IsRegistered(null).Should().BeFalse();
    }

    /// <summary>
    /// IsRegistered 对空字符串应返回 false。
    /// </summary>
    [Fact]
    public void IsRegistered_EmptyString_ShouldReturnFalse()
    {
        ErrorCodeCatalog.IsRegistered("").Should().BeFalse();
    }

    /// <summary>
    /// FallbackResourceKey 常量应为 "generic_error"。
    /// </summary>
    [Fact]
    public void FallbackResourceKey_ShouldBeGenericError()
    {
        ErrorCodeCatalog.FallbackResourceKey.Should().Be("generic_error");
    }
}
