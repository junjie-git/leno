using FluentAssertions;
using Leno.Infrastructure.Middleware;
using Xunit;

namespace Leno.Infrastructure.Tests.Middleware;

public class ErrorCodeMappingTests
{
    [Theory]
    [InlineData("USER_NOT_FOUND", 404)]
    [InlineData("CART_ITEM_NOT_FOUND", 404)]
    [InlineData("SHOP_ALREADY_EXISTS", 409)]
    [InlineData("ANNOUNCEMENT_ALREADY_PUBLISHED", 409)]
    [InlineData("USER_USERNAME_EXISTS", 409)]
    [InlineData("TASK_CONFLICT", 409)]
    [InlineData("ADDRESS_FORBIDDEN", 403)]
    [InlineData("REVIEW_FORBIDDEN", 403)]
    [InlineData("CART_PRICE_UNAVAILABLE", 503)]
    [InlineData("OAUTH_TOKEN_EXCHANGE_FAILED", 502)]
    [InlineData("OAUTH_USERINFO_FAILED", 502)]
    [InlineData("OAUTH_CONFIG_MISSING", 500)]
    [InlineData("USER_2FA_SECRET_MISSING", 500)]
    [InlineData("OAUTH_STATE_EXPIRED", 401)]
    [InlineData("CART_USER_REQUIRED", 401)]
    public void GetStatusCode_WithSuffixConvention_ShouldInferCorrectly(string errorCode, int expected)
    {
        ErrorCodeMapping.Reset();
        var actual = ErrorCodeMapping.GetStatusCode(errorCode);
        actual.Should().Be(expected);
    }

    [Theory]
    [InlineData("USER_NO_LOGIN_METHOD")]
    [InlineData("USER_PASSWORD_SAME")]
    [InlineData("METRICS_INVALID_RANGE")]
    [InlineData("UNKNOWN_ERROR")]
    [InlineData("")]
    [InlineData(null)]
    public void GetStatusCode_WithUnmatchedSuffix_ShouldReturn400(string? errorCode)
    {
        ErrorCodeMapping.Reset();
        var actual = ErrorCodeMapping.GetStatusCode(errorCode);
        actual.Should().Be(400);
    }

    [Fact]
    public void Register_ShouldOverrideSuffixConvention()
    {
        ErrorCodeMapping.Reset();
        ErrorCodeMapping.Register("USER_DISABLED", 403);

        var actual = ErrorCodeMapping.GetStatusCode("USER_DISABLED");

        actual.Should().Be(403);
    }

    [Fact]
    public void Register_ShouldTakePrecedenceOverSuffix()
    {
        ErrorCodeMapping.Reset();
        // USER_NOT_FOUND 按后缀应为 404，显式注册为 410 Gone
        ErrorCodeMapping.Register("USER_NOT_FOUND", 410);

        var actual = ErrorCodeMapping.GetStatusCode("USER_NOT_FOUND");

        actual.Should().Be(410);
    }

    [Fact]
    public void RegisterAll_ShouldRegisterMultipleEntries()
    {
        ErrorCodeMapping.Reset();
        ErrorCodeMapping.RegisterAll(
            ("USER_DISABLE_SELF", 409),
            ("USER_REVOKE_ADMIN_SELF", 409),
            ("USER_LAST_ROLE", 409),
            ("EXTERNAL_LOGIN_LAST", 409),
            ("CART_VARIETY_LIMIT", 409),
            ("SELLER_APPROVED", 409),
            ("SHOP_CLOSED", 409),
            ("ADDRESS_ALREADY_DELETED", 409),
            ("ADDRESS_NOT_ACTIVE", 409),
            ("USER_DISABLED", 403));

        ErrorCodeMapping.GetStatusCode("USER_DISABLE_SELF").Should().Be(409);
        ErrorCodeMapping.GetStatusCode("USER_DISABLED").Should().Be(403);
        ErrorCodeMapping.GetStatusCode("CART_VARIETY_LIMIT").Should().Be(409);
    }
}
