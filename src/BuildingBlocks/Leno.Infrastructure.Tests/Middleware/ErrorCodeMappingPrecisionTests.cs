using FluentAssertions;
using Leno.Infrastructure.Middleware;
using Xunit;

namespace Leno.Infrastructure.Tests.Middleware;

/// <summary>
/// ErrorCodeMapping 精确匹配与缓存修复验证。
/// T33：Contains 子串匹配 → EndsWith 精确后缀匹配 + token 匹配中间标记。
/// T34：静态 ConcurrentDictionary 无清理 → MemoryCache + SizeLimit 限制增长。
/// </summary>
public class ErrorCodeMappingPrecisionTests
{
    [Theory]
    [InlineData("NOT_FOUND_USER", 400, "不以 _NOT_FOUND 结尾，不应误匹配")]
    [InlineData("EXISTS_USER", 400, "不以 _EXISTS 结尾，不应误匹配")]
    [InlineData("FAILED_OPERATION", 400, "不以 _FAILED 结尾，不应误匹配")]
    [InlineData("FORBIDDEN_USER_ACCESS", 400, "不以 _FORBIDDEN 结尾，不应误匹配")]
    [InlineData("MISSING_DATA_REPORT", 400, "不以 _MISSING 结尾，不应误匹配")]
    [InlineData("EXPIRED_TOKEN_HANDLER", 400, "不以 _EXPIRED 结尾，不应误匹配")]
    [InlineData("REQUIRED_FIELD_VALIDATOR", 400, "不以 _REQUIRED 结尾，不应误匹配")]
    [InlineData("CONFLICT_DETECTOR", 400, "不以 _CONFLICT 结尾，不应误匹配")]
    [InlineData("UNAVAILABLE_SERVICE_HANDLER", 400, "不以 _UNAVAILABLE 结尾，不应误匹配")]
    public void GetStatusCode_SuffixInMiddle_ShouldNotMatch(string errorCode, int expected, string reason)
    {
        // Arrange
        ErrorCodeMapping.Reset();

        // Act
        var actual = ErrorCodeMapping.GetStatusCode(errorCode);

        // Assert — EndsWith 精确匹配，后缀出现在中间不应触发
        actual.Should().Be(expected, reason);
    }

    [Theory]
    [InlineData("USER_NOT_FOUND", 404)]
    [InlineData("CART_ITEM_NOT_FOUND", 404)]
    [InlineData("ORDER_EXISTS", 409)]
    [InlineData("TASK_CONFLICT", 409)]
    [InlineData("ADDRESS_FORBIDDEN", 403)]
    [InlineData("SERVICE_UNAVAILABLE", 503)]
    [InlineData("OAUTH_FAILED", 502)]
    [InlineData("CONFIG_MISSING", 500)]
    [InlineData("TOKEN_EXPIRED", 401)]
    [InlineData("AUTH_REQUIRED", 401)]
    public void GetStatusCode_TrueSuffix_ShouldMatch(string errorCode, int expected)
    {
        // Arrange
        ErrorCodeMapping.Reset();

        // Act
        var actual = ErrorCodeMapping.GetStatusCode(errorCode);

        // Assert — 以后缀结尾的 ErrorCode 应正确匹配
        actual.Should().Be(expected);
    }

    [Theory]
    [InlineData("SHOP_ALREADY_EXISTS", 409)]
    [InlineData("ANNOUNCEMENT_ALREADY_PUBLISHED", 409)]
    [InlineData("USER_ALREADY_VERIFIED", 409)]
    [InlineData("ACCOUNT_ALREADY_CONFIRMED", 409)]
    public void GetStatusCode_AlreadyToken_ShouldMatchViaTokenLookup(string errorCode, int expected)
    {
        // Arrange
        ErrorCodeMapping.Reset();

        // Act
        var actual = ErrorCodeMapping.GetStatusCode(errorCode);

        // Assert — _ALREADY_ 是中间标记，通过 token 精确匹配 ALREADY
        actual.Should().Be(expected, "_ALREADY_ 应通过 token 匹配中间出现的 ALREADY");
    }

    [Theory]
    [InlineData("READY_TO_SHIP", 400, "READY 包含 ALREADY 子串但不应匹配 token")]
    [InlineData("ALREADINESS_CHECK", 400, "ALREADINESS 包含 ALREADY 子串但不应匹配 token")]
    public void GetStatusCode_AlreadySubstringButNotToken_ShouldNotMatch(string errorCode, int expected, string reason)
    {
        // Arrange
        ErrorCodeMapping.Reset();

        // Act
        var actual = ErrorCodeMapping.GetStatusCode(errorCode);

        // Assert — token 匹配按 '_' 分割，子串包含不应触发
        actual.Should().Be(expected, reason);
    }

    [Fact]
    public void Register_AfterReset_ShouldNotReturnStaleEntry()
    {
        // Arrange — 验证 T34：MemoryCache.Compact(1.0) 清空全部条目
        ErrorCodeMapping.Reset();
        ErrorCodeMapping.Register("CUSTOM_ERROR", 422);
        ErrorCodeMapping.GetStatusCode("CUSTOM_ERROR").Should().Be(422);

        // Act — Reset 后注册应清空旧条目
        ErrorCodeMapping.Reset();

        // Assert — Reset 后 CUSTOM_ERROR 不再命中显式注册，回退到后缀/默认
        ErrorCodeMapping.GetStatusCode("CUSTOM_ERROR").Should().Be(400,
            "Reset 后显式注册表应清空，CUSTOM_ERROR 不匹配任何后缀规则");
    }

    [Fact]
    public void Register_OverwriteExisting_ShouldUpdateStatusCode()
    {
        // Arrange
        ErrorCodeMapping.Reset();
        ErrorCodeMapping.Register("DYNAMIC_ERROR", 409);

        // Act — 覆盖注册
        ErrorCodeMapping.Register("DYNAMIC_ERROR", 422);

        // Assert
        ErrorCodeMapping.GetStatusCode("DYNAMIC_ERROR").Should().Be(422,
            "重复注册同一 ErrorCode 应覆盖旧值");
    }

    [Fact]
    public void Register_MultipleEntries_AllShouldBeRetrievable()
    {
        // Arrange — 验证 T34：MemoryCache 在 SizeLimit 内可存储多条目
        ErrorCodeMapping.Reset();
        var entries = Enumerable.Range(0, 100)
            .Select(i => ($"DYNAMIC_ERROR_{i}", 400 + (i % 10)))
            .ToArray();

        // Act
        ErrorCodeMapping.RegisterAll(entries);

        // Assert — 全部可检索
        foreach (var (code, status) in entries)
        {
            ErrorCodeMapping.GetStatusCode(code).Should().Be(status,
                "注册的条目应全部可检索");
        }
    }

    [Fact]
    public void GetStatusCode_ExplicitRegistrationTakesPrecedenceOverSuffix()
    {
        // Arrange
        ErrorCodeMapping.Reset();
        // USER_NOT_FOUND 按后缀为 404，显式注册为 451
        ErrorCodeMapping.Register("USER_NOT_FOUND", 451);

        // Act
        var actual = ErrorCodeMapping.GetStatusCode("USER_NOT_FOUND");

        // Assert
        actual.Should().Be(451, "显式注册优先于后缀推断");
    }
}
