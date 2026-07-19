using FluentAssertions;
using Grpc.Core;
using Leno.Infrastructure.AntiCorruption;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Leno.Infrastructure.Tests.AntiCorruption;

public class GrpcAntiCorruptionClientBaseTests
{
    private sealed class TestGrpcClient : GrpcAntiCorruptionClientBase
    {
        protected override string ServiceName => "test_service";

        public Task<T> RunExecuteAsync<T>(string operation, Func<CancellationToken, Task<T>> fn, CancellationToken ct = default)
            => ExecuteAsync(operation, fn, ct);
    }

    [Fact]
    public async Task Unavailable_RpcException_Preserved_As_InnerException()
    {
        var client = new TestGrpcClient();
        var rpcEx = new RpcException(new Status(StatusCode.Unavailable, "connection refused"));

        var act = async () => await client.RunExecuteAsync("op", _ => Task.FromException<int>(rpcEx));

        var thrown = (await act.Should().ThrowAsync<AntiCorruptionException>()).Which;
        thrown.InnerException.Should().BeSameAs(rpcEx);
        thrown.ErrorCode.Should().Be("TEST_SERVICE_UNAVAILABLE");
    }

    [Fact]
    public async Task DeadlineExceeded_RpcException_Preserved_As_InnerException()
    {
        var client = new TestGrpcClient();
        var rpcEx = new RpcException(new Status(StatusCode.DeadlineExceeded, "timeout"));

        var act = async () => await client.RunExecuteAsync("op", _ => Task.FromException<int>(rpcEx));

        var thrown = (await act.Should().ThrowAsync<AntiCorruptionException>()).Which;
        thrown.InnerException.Should().BeSameAs(rpcEx);
        thrown.ErrorCode.Should().Be("TEST_SERVICE_UNAVAILABLE");
    }

    [Fact]
    public async Task NotFound_RpcException_Preserved_As_InnerException_BusinessException()
    {
        var client = new TestGrpcClient();
        var rpcEx = new RpcException(new Status(StatusCode.NotFound, "sku not found"));

        var act = async () => await client.RunExecuteAsync("op", _ => Task.FromException<int>(rpcEx));

        var thrown = (await act.Should().ThrowAsync<AntiCorruptionException>()).Which;
        thrown.InnerException.Should().BeSameAs(rpcEx);
        thrown.ErrorCode.Should().Be("TEST_SERVICE_REMOTE_FAILED");
    }

    [Fact]
    public async Task UserCancellation_Propagates_WithoutWrapping()
    {
        var client = new TestGrpcClient();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await client.RunExecuteAsync("op", ct => Task.FromException<int>(new OperationCanceledException(ct)), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
