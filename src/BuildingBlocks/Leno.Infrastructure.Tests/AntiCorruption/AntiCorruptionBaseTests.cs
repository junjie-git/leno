using System.Net;
using System.Net.Http;
using Leno.Infrastructure.AntiCorruption;
using Leno.SharedKernel.Exceptions;
using FluentAssertions;
using Xunit;

namespace Leno.Infrastructure.Tests.AntiCorruption;

public class AntiCorruptionBaseTests
{
    private sealed class TestAntiCorruption : AntiCorruptionBase
    {
        protected override string ServiceName => "test_service";

        public Task<T> RunExecuteAsync<T>(string op, Func<CancellationToken, Task<T>> fn, CancellationToken ct = default)
            => ExecuteAsync(op, fn, ct);

        public void RunEnsureSuccess(HttpResponseMessage resp, string op) => EnsureSuccessStatusCode(resp, op);
    }

    private sealed class TestDomainException : DomainException
    {
        public TestDomainException(string message, string errorCode = "TEST_ERROR")
            : base(message, errorCode) { }
    }

    [Fact]
    public async Task ExecuteAsync_Success_ReturnsValue()
    {
        var svc = new TestAntiCorruption();
        var result = await svc.RunExecuteAsync("op", _ => Task.FromResult(42));
        result.Should().Be(42);
    }

    [Fact]
    public async Task ExecuteAsync_HttpRequestException_ThrowsUnavailable()
    {
        var svc = new TestAntiCorruption();
        var act = () => svc.RunExecuteAsync<int>("op", _ => throw new HttpRequestException("connection refused"));

        var ex = await act.Should().ThrowAsync<DomainException>();
        ex.Which.ErrorCode.Should().Be("TEST_SERVICE_UNAVAILABLE");
    }

    [Fact]
    public async Task ExecuteAsync_Timeout_ThrowsUnavailable()
    {
        var svc = new TestAntiCorruption();
        var act = () => svc.RunExecuteAsync<int>("op", _ => throw new OperationCanceledException("timeout"));

        var ex = await act.Should().ThrowAsync<DomainException>();
        ex.Which.ErrorCode.Should().Be("TEST_SERVICE_UNAVAILABLE");
    }

    [Fact]
    public async Task ExecuteAsync_GenericException_ThrowsRemoteFailed()
    {
        var svc = new TestAntiCorruption();
        var act = () => svc.RunExecuteAsync<int>("op", _ => throw new InvalidOperationException("boom"));

        var ex = await act.Should().ThrowAsync<DomainException>();
        ex.Which.ErrorCode.Should().Be("TEST_SERVICE_REMOTE_FAILED");
    }

    [Fact]
    public async Task ExecuteAsync_DomainException_Passthrough()
    {
        var svc = new TestAntiCorruption();
        var domainEx = new TestDomainException("biz", "TEST_SERVICE_BUSINESS_ERROR");
        var act = () => svc.RunExecuteAsync<int>("op", _ => throw domainEx);

        var ex = await act.Should().ThrowAsync<DomainException>();
        ex.Which.ErrorCode.Should().Be("TEST_SERVICE_BUSINESS_ERROR");
    }

    [Fact]
    public void EnsureSuccessStatusCode_NonSuccess_ThrowsRemoteFailed()
    {
        var svc = new TestAntiCorruption();
        using var resp = new HttpResponseMessage(HttpStatusCode.InternalServerError);
        var act = () => svc.RunEnsureSuccess(resp, "op");

        var ex = act.Should().Throw<DomainException>();
        ex.Which.ErrorCode.Should().Be("TEST_SERVICE_REMOTE_FAILED");
    }

    [Fact]
    public void EnsureSuccessStatusCode_Success_DoesNotThrow()
    {
        var svc = new TestAntiCorruption();
        using var resp = new HttpResponseMessage(HttpStatusCode.OK);
        var act = () => svc.RunEnsureSuccess(resp, "op");
        act.Should().NotThrow();
    }
}
