using System.Collections.Concurrent;
using System.Net;
using FluentAssertions;
using Leno.ApiGateway.Bff;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Leno.ApiGateway.Tests.Bff;

/// <summary>
/// <see cref="BffAuthorizationForwardingHandler"/> 单元测试：验证调用方 Bearer token 被透传到下游请求。
/// </summary>
public sealed class BffAuthorizationForwardingHandlerTests
{
    [Fact]
    public async Task SendAsync_IncomingBearerToken_IsCopiedToDownstreamRequest()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Authorization = "Bearer caller-token";

        var capture = new CaptureHandler();
        var forwardingHandler = new BffAuthorizationForwardingHandler(
            new HttpContextAccessor { HttpContext = httpContext });
        forwardingHandler.InnerHandler = capture;

        using var invoker = new HttpMessageInvoker(forwardingHandler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://product-api:8080/api/products/x");

        await invoker.SendAsync(request, CancellationToken.None);

        var forwarded = capture.Calls.Should().ContainSingle().Subject;
        forwarded.Headers.Authorization.Should().NotBeNull();
        forwarded.Headers.Authorization!.Scheme.Should().Be("Bearer");
        forwarded.Headers.Authorization.Parameter.Should().Be("caller-token");
    }

    [Fact]
    public async Task SendAsync_NoIncomingAuthorization_LeavesDownstreamRequestAnonymous()
    {
        var capture = new CaptureHandler();
        var forwardingHandler = new BffAuthorizationForwardingHandler(
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() });
        forwardingHandler.InnerHandler = capture;

        using var invoker = new HttpMessageInvoker(forwardingHandler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://review-api:8080/api/products/x/reviews");

        await invoker.SendAsync(request, CancellationToken.None);

        capture.Calls.Should().ContainSingle().Subject.Headers.Authorization.Should().BeNull();
    }

    [Fact]
    public async Task SendAsync_RequestAlreadyHasAuthorization_DoesNotOverwrite()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Authorization = "Bearer caller-token";

        var capture = new CaptureHandler();
        var forwardingHandler = new BffAuthorizationForwardingHandler(
            new HttpContextAccessor { HttpContext = httpContext });
        forwardingHandler.InnerHandler = capture;

        using var invoker = new HttpMessageInvoker(forwardingHandler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://order-api:8080/api/orders/1");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "explicit-token");

        await invoker.SendAsync(request, CancellationToken.None);

        capture.Calls.Should().ContainSingle().Subject.Headers.Authorization!.Parameter
            .Should().Be("explicit-token");
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        private readonly ConcurrentBag<HttpRequestMessage> _calls = new();

        public IEnumerable<HttpRequestMessage> Calls => _calls;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            _calls.Add(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
