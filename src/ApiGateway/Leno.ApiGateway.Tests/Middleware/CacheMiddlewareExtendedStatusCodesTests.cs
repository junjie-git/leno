using Leno.ApiGateway.Middleware;
using Microsoft.AspNetCore.Http;

namespace Leno.ApiGateway.Tests.Middleware;

/// <summary>
/// P1-T17 验证：CacheMiddleware IsCacheableResponse 扩展可缓存状态码。
/// <para>
/// T17：除 200 外，203/204/206/300/301/405/410/414/501 也应可缓存。
/// 404 因既有测试 IsCacheableResponse_With404_ReturnsFalse 断言不可缓存，标记 [SKIPPED-CONFLICT]。
/// </para>
/// </summary>
public class CacheMiddlewareExtendedStatusCodesTests
{
    [Theory]
    [InlineData(200)]
    [InlineData(203)]
    [InlineData(204)]
    [InlineData(206)]
    [InlineData(300)]
    [InlineData(301)]
    [InlineData(405)]
    [InlineData(410)]
    [InlineData(414)]
    [InlineData(501)]
    public void IsCacheableResponse_ExtendedStatusCodes_ReturnsTrue(int statusCode)
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Response.StatusCode = statusCode;

        // Act
        var result = CacheMiddleware.IsCacheableResponse(httpContext.Response);

        // Assert — T17：扩展的状态码应可缓存
        result.Should().BeTrue(
            "状态码 {0} 应在可缓存集合中（T17 扩展）", statusCode);
    }

    [Theory]
    [InlineData(302)]  // 302 临时重定向不可缓存（与 301 永久重定向区分）
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    public void IsCacheableResponse_NonCacheableStatusCodes_ReturnsFalse(int statusCode)
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Response.StatusCode = statusCode;

        // Act
        var result = CacheMiddleware.IsCacheableResponse(httpContext.Response);

        // Assert
        result.Should().BeFalse(
            "状态码 {0} 不在可缓存集合中", statusCode);
    }

    [Fact]
    public void IsCacheableResponse_WithNoStoreHeader_ReturnsFalse()
    {
        // Arrange — 200 + Cache-Control: no-store 应不可缓存
        var httpContext = new DefaultHttpContext();
        httpContext.Response.StatusCode = 200;
        httpContext.Response.Headers["Cache-Control"] = "no-store";

        // Act
        var result = CacheMiddleware.IsCacheableResponse(httpContext.Response);

        // Assert
        result.Should().BeFalse("no-store 指令应阻止缓存");
    }

    [Fact]
    public void IsCacheableResponse_301WithNoCacheControl_ReturnsTrue()
    {
        // Arrange — 301 永久重定向应可缓存
        var httpContext = new DefaultHttpContext();
        httpContext.Response.StatusCode = 301;

        // Act
        var result = CacheMiddleware.IsCacheableResponse(httpContext.Response);

        // Assert
        result.Should().BeTrue("301 永久重定向应可缓存（T17 扩展）");
    }

    [Fact]
    public void CacheableStatusCodes_ContainsExpectedSet()
    {
        // Assert — 验证可缓存状态码集合包含 T17 规定的状态码（不含 404，因既有测试冲突）
        CacheMiddleware.CacheableStatusCodes.Should().Contain(new[] { 200, 203, 204, 206, 300, 301, 405, 410, 414, 501 });
        CacheMiddleware.CacheableStatusCodes.Should().NotContain(404,
            "404 因既有测试 IsCacheableResponse_With404_ReturnsFalse 断言不可缓存，标记 [SKIPPED-CONFLICT]");
    }
}
