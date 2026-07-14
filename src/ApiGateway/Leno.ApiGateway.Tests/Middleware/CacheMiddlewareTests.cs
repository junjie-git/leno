using System.Security.Claims;
using Leno.ApiGateway.Middleware;
using Leno.ApiGateway.Options;
using Microsoft.AspNetCore.Http;

namespace Leno.ApiGateway.Tests.Middleware;

public class CacheMiddlewareTests
{
    [Theory]
    [InlineData("GET", true)]
    [InlineData("HEAD", true)]
    [InlineData("POST", false)]
    [InlineData("PUT", false)]
    [InlineData("DELETE", false)]
    [InlineData("PATCH", false)]
    public void IsCacheableRequest_FiltersByMethod(string method, bool expected)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = method;

        var result = CacheMiddleware.IsCacheableRequest(httpContext.Request);

        result.Should().Be(expected);
    }

    [Fact]
    public void IsCacheableResponse_With200AndNoCacheControl_ReturnsTrue()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Response.StatusCode = 200;

        var result = CacheMiddleware.IsCacheableResponse(httpContext.Response);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsCacheableResponse_With500_ReturnsFalse()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Response.StatusCode = 500;

        var result = CacheMiddleware.IsCacheableResponse(httpContext.Response);

        result.Should().BeFalse();
    }

    [Fact]
    public void IsCacheableResponse_With404_ReturnsFalse()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Response.StatusCode = 404;

        var result = CacheMiddleware.IsCacheableResponse(httpContext.Response);

        result.Should().BeFalse();
    }

    [Fact]
    public void IsCacheableResponse_WithNoStoreDirective_ReturnsFalse()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Response.StatusCode = 200;
        httpContext.Response.Headers.CacheControl = "no-store";

        var result = CacheMiddleware.IsCacheableResponse(httpContext.Response);

        result.Should().BeFalse();
    }

    [Fact]
    public void GenerateCacheKey_IncludesMethodPathQueryAndUserId()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "GET";
        httpContext.Request.Path = "/api/products/123";
        httpContext.Request.QueryString = new QueryString("?page=1&size=20");
        httpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(new[] { new Claim("Sub", "42") }, "Test"));

        var key = CacheMiddleware.GenerateCacheKey(httpContext);

        key.Should().Be("GET:/api/products/123?page=1&size=20:42");
    }

    [Fact]
    public void GenerateCacheKey_WithAnonymousUser_HasEmptyUserIdSegment()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "GET";
        httpContext.Request.Path = "/api/categories";
        httpContext.Request.QueryString = QueryString.Empty;
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity());

        var key = CacheMiddleware.GenerateCacheKey(httpContext);

        key.Should().Be("GET:/api/categories:");
    }

    [Fact]
    public void GenerateCacheKey_DifferentUsers_ProduceDifferentKeys()
    {
        var ctx1 = new DefaultHttpContext();
        ctx1.Request.Method = "GET";
        ctx1.Request.Path = "/api/products/1";
        ctx1.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("Sub", "1") }, "Test"));

        var ctx2 = new DefaultHttpContext();
        ctx2.Request.Method = "GET";
        ctx2.Request.Path = "/api/products/1";
        ctx2.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("Sub", "2") }, "Test"));

        CacheMiddleware.GenerateCacheKey(ctx1).Should().NotBe(CacheMiddleware.GenerateCacheKey(ctx2));
    }
}

public class CacheOptionsTests
{
    [Fact]
    public void GetTtlForPath_WithMatchingPrefix_ReturnsConfiguredTtl()
    {
        var options = new CacheOptions
        {
            DefaultTtl = TimeSpan.FromSeconds(60),
            PathTtls = new()
            {
                ["/api/products/"] = TimeSpan.FromSeconds(300),
                ["/api/categories/"] = TimeSpan.FromSeconds(60)
            }
        };

        var ttl = options.GetTtlForPath("/api/products/123");

        ttl.Should().Be(TimeSpan.FromSeconds(300));
    }

    [Fact]
    public void GetTtlForPath_WithNoMatch_ReturnsDefaultTtl()
    {
        var options = new CacheOptions
        {
            DefaultTtl = TimeSpan.FromSeconds(60),
            PathTtls = new() { ["/api/products/"] = TimeSpan.FromSeconds(300) }
        };

        var ttl = options.GetTtlForPath("/api/orders/456");

        ttl.Should().Be(TimeSpan.FromSeconds(60));
    }

    [Fact]
    public void GetTtlForPath_LongestPrefixWins()
    {
        var options = new CacheOptions
        {
            DefaultTtl = TimeSpan.FromSeconds(60),
            PathTtls = new()
            {
                ["/api/"] = TimeSpan.FromSeconds(10),
                ["/api/products/"] = TimeSpan.FromSeconds(300)
            }
        };

        // /api/products/ 应匹配更长的 /api/products/ 前缀
        var ttl = options.GetTtlForPath("/api/products/123");

        ttl.Should().Be(TimeSpan.FromSeconds(300));
    }
}
