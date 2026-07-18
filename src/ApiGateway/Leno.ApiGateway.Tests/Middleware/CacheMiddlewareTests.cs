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

        // 仅设置 Sub claim，role/shopId 退化为默认值 guest/none
        key.Should().Be("GET:/api/products/123?page=1&size=20:42:guest:none");
    }

    [Fact]
    public void GenerateCacheKey_WithAnonymousUser_UsesDefaults()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "GET";
        httpContext.Request.Path = "/api/categories";
        httpContext.Request.QueryString = QueryString.Empty;
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity());

        var key = CacheMiddleware.GenerateCacheKey(httpContext);

        key.Should().Be("GET:/api/categories:anonymous:guest:none");
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

    [Fact]
    public void GenerateCacheKey_IncludesRole()
    {
        var ctx1 = new DefaultHttpContext();
        ctx1.Request.Method = "GET";
        ctx1.Request.Path = "/api/products/1";
        ctx1.User = new ClaimsPrincipal(
            new ClaimsIdentity(new[]
            {
                new Claim("Sub", "42"),
                new Claim("Role", "customer")
            }, "Test"));

        var ctx2 = new DefaultHttpContext();
        ctx2.Request.Method = "GET";
        ctx2.Request.Path = "/api/products/1";
        ctx2.User = new ClaimsPrincipal(
            new ClaimsIdentity(new[]
            {
                new Claim("Sub", "42"),
                new Claim("Role", "admin")
            }, "Test"));

        var key1 = CacheMiddleware.GenerateCacheKey(ctx1);
        var key2 = CacheMiddleware.GenerateCacheKey(ctx2);

        key1.Should().Be("GET:/api/products/1:42:customer:none");
        key2.Should().Be("GET:/api/products/1:42:admin:none");
        key1.Should().NotBe(key2);
    }

    [Fact]
    public void GenerateCacheKey_IncludesShopId()
    {
        var ctx1 = new DefaultHttpContext();
        ctx1.Request.Method = "GET";
        ctx1.Request.Path = "/api/bff/seller/orders";
        ctx1.User = new ClaimsPrincipal(
            new ClaimsIdentity(new[]
            {
                new Claim("Sub", "42"),
                new Claim("Role", "seller"),
                new Claim("shop_id", "shop-a")
            }, "Test"));

        var ctx2 = new DefaultHttpContext();
        ctx2.Request.Method = "GET";
        ctx2.Request.Path = "/api/bff/seller/orders";
        ctx2.User = new ClaimsPrincipal(
            new ClaimsIdentity(new[]
            {
                new Claim("Sub", "42"),
                new Claim("Role", "seller"),
                new Claim("shop_id", "shop-b")
            }, "Test"));

        var key1 = CacheMiddleware.GenerateCacheKey(ctx1);
        var key2 = CacheMiddleware.GenerateCacheKey(ctx2);

        key1.Should().Be("GET:/api/bff/seller/orders:42:seller:shop-a");
        key2.Should().Be("GET:/api/bff/seller/orders:42:seller:shop-b");
        key1.Should().NotBe(key2);
    }

    [Fact]
    public void GenerateCacheKey_DifferentUsersGenerateDifferentKeys()
    {
        // 综合验证：相同 path/query 但 userId/role/shopId 全部不同 → Key 不同
        var ctx1 = new DefaultHttpContext();
        ctx1.Request.Method = "GET";
        ctx1.Request.Path = "/api/bff/seller/dashboard";
        ctx1.Request.QueryString = new QueryString("?range=30d");
        ctx1.User = new ClaimsPrincipal(
            new ClaimsIdentity(new[]
            {
                new Claim("Sub", "user-1"),
                new Claim("Role", "seller"),
                new Claim("shop_id", "shop-1")
            }, "Test"));

        var ctx2 = new DefaultHttpContext();
        ctx2.Request.Method = "GET";
        ctx2.Request.Path = "/api/bff/seller/dashboard";
        ctx2.Request.QueryString = new QueryString("?range=30d");
        ctx2.User = new ClaimsPrincipal(
            new ClaimsIdentity(new[]
            {
                new Claim("Sub", "user-2"),
                new Claim("Role", "admin"),
                new Claim("shop_id", "shop-2")
            }, "Test"));

        var key1 = CacheMiddleware.GenerateCacheKey(ctx1);
        var key2 = CacheMiddleware.GenerateCacheKey(ctx2);

        key1.Should().Be("GET:/api/bff/seller/dashboard?range=30d:user-1:seller:shop-1");
        key2.Should().Be("GET:/api/bff/seller/dashboard?range=30d:user-2:admin:shop-2");
        key1.Should().NotBe(key2);
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
