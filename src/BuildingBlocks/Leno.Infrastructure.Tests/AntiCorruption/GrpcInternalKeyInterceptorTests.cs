using FluentAssertions;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Leno.Infrastructure.AntiCorruption;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Leno.Infrastructure.Tests.AntiCorruption;

public class GrpcInternalKeyInterceptorTests
{
    private sealed class FakeContext : ServerCallContext
    {
        private readonly Metadata _requestHeaders;
        public FakeContext(Metadata requestHeaders) => _requestHeaders = requestHeaders;
        protected override string MethodCore => "/test/Method";
        protected override string HostCore => "localhost";
        protected override string PeerCore => "peer";
        protected override DateTime DeadlineCore => DateTime.UtcNow.AddMinutes(1);
        protected override Metadata RequestHeadersCore => _requestHeaders;
        protected override Metadata ResponseTrailersCore { get; } = new();
        protected override Status StatusCore { get; set; }
        protected override WriteOptions? WriteOptionsCore { get; set; }
        protected override CancellationToken CancellationTokenCore => CancellationToken.None;
        protected override AuthContext AuthContextCore => new AuthContext(null, new Dictionary<string, List<AuthProperty>>());
        protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options) => null!;
        protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) => Task.CompletedTask;
    }

    private static IOptionsMonitor<AntiCorruptionOptions> CreateOptionsMonitor(string? internalKey)
    {
        var mock = new Mock<IOptionsMonitor<AntiCorruptionOptions>>();
        mock.SetupGet(o => o.CurrentValue).Returns(new AntiCorruptionOptions { InternalApiKey = internalKey });
        return mock.Object;
    }

    private static Task<TResponse> Continuation<TRequest, TResponse>(TRequest req, ServerCallContext ctx)
        where TResponse : class, new()
        => Task.FromResult(new TResponse());

    [Fact]
    public async Task Valid_InternalKey_CallsContinuation()
    {
        var interceptor = new GrpcInternalKeyInterceptor(
            CreateOptionsMonitor("secret-key"),
            NullLogger<GrpcInternalKeyInterceptor>.Instance);
        var headers = new Metadata { { "x-internal-key", "secret-key" } };
        var ctx = new FakeContext(headers);

        var act = async () => await interceptor.UnaryServerHandler(
            new object(), ctx,
            (req, c) => Continuation<object, TestResponse>(req, c));

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Missing_InternalKey_ThrowsUnauthenticated()
    {
        var interceptor = new GrpcInternalKeyInterceptor(
            CreateOptionsMonitor("secret-key"),
            NullLogger<GrpcInternalKeyInterceptor>.Instance);
        var ctx = new FakeContext(new Metadata());

        var act = async () => await interceptor.UnaryServerHandler(
            new object(), ctx,
            (req, c) => Continuation<object, TestResponse>(req, c));

        var thrown = (await act.Should().ThrowAsync<RpcException>()).Which;
        thrown.StatusCode.Should().Be(StatusCode.Unauthenticated);
    }

    [Fact]
    public async Task Wrong_InternalKey_ThrowsUnauthenticated()
    {
        var interceptor = new GrpcInternalKeyInterceptor(
            CreateOptionsMonitor("secret-key"),
            NullLogger<GrpcInternalKeyInterceptor>.Instance);
        var headers = new Metadata { { "x-internal-key", "wrong-key" } };
        var ctx = new FakeContext(headers);

        var act = async () => await interceptor.UnaryServerHandler(
            new object(), ctx,
            (req, c) => Continuation<object, TestResponse>(req, c));

        var thrown = (await act.Should().ThrowAsync<RpcException>()).Which;
        thrown.StatusCode.Should().Be(StatusCode.Unauthenticated);
    }

    [Fact]
    public async Task CaseInsensitive_HeaderMatching()
    {
        var interceptor = new GrpcInternalKeyInterceptor(
            CreateOptionsMonitor("secret-key"),
            NullLogger<GrpcInternalKeyInterceptor>.Instance);
        var headers = new Metadata { { "X-Internal-Key", "secret-key" } };  // 大写
        var ctx = new FakeContext(headers);

        var act = async () => await interceptor.UnaryServerHandler(
            new object(), ctx,
            (req, c) => Continuation<object, TestResponse>(req, c));

        await act.Should().NotThrowAsync();
    }

    private sealed class TestResponse { public string Value { get; set; } = string.Empty; }
}
