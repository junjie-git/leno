using FluentAssertions;
using Grpc.Core;
using Leno.Infrastructure.AntiCorruption;
using Leno.SharedKernel.Exceptions;
using Leno.Testing.Builders;
using Xunit;

namespace Leno.Infrastructure.Tests.AntiCorruption;

public class GrpcAntiCorruptionClientBaseTests
{
    private sealed class TestGrpcClient : GrpcAntiCorruptionClientBase
    {
        protected override string ServiceName => "test_service";

        public TestGrpcClient(IServiceProvider serviceProvider)
            : base(serviceProvider)
        {
        }

        public Task<T> RunExecuteAsync<T>(string operation, Func<CancellationToken, Task<T>> fn, CancellationToken ct = default)
            => ExecuteAsync(operation, fn, ct);
    }

    private sealed class TestDomainException : DomainException
    {
        public TestDomainException(string message, string errorCode = "TEST_DOMAIN_ERROR")
            : base(message, errorCode) { }
    }

    /// <summary>
    /// 构造测试用 <see cref="IServiceProvider"/>，注册零延迟的 gRPC retry 策略，
    /// 避免测试因指数退避产生秒级延迟。重试次数与生产一致（2 次）。
    /// </summary>
    private static IServiceProvider BuildServiceProvider(int retryCount = 2)
        => GrpcAntiCorruptionTestHelper.BuildServiceProviderWithGrpcRetry(retryCount);

    [Fact]
    public async Task Unavailable_RpcException_Preserved_As_InnerException()
    {
        var client = new TestGrpcClient(BuildServiceProvider());
        var rpcEx = new RpcException(new Status(StatusCode.Unavailable, "connection refused"));

        var act = async () => await client.RunExecuteAsync("op", _ => Task.FromException<int>(rpcEx));

        var thrown = (await act.Should().ThrowAsync<AntiCorruptionException>()).Which;
        thrown.InnerException.Should().BeSameAs(rpcEx);
        thrown.ErrorCode.Should().Be("TEST_SERVICE_UNAVAILABLE");
    }

    [Fact]
    public async Task DeadlineExceeded_RpcException_Preserved_As_InnerException()
    {
        var client = new TestGrpcClient(BuildServiceProvider());
        var rpcEx = new RpcException(new Status(StatusCode.DeadlineExceeded, "timeout"));

        var act = async () => await client.RunExecuteAsync("op", _ => Task.FromException<int>(rpcEx));

        var thrown = (await act.Should().ThrowAsync<AntiCorruptionException>()).Which;
        thrown.InnerException.Should().BeSameAs(rpcEx);
        thrown.ErrorCode.Should().Be("TEST_SERVICE_UNAVAILABLE");
    }

    [Fact]
    public async Task NotFound_RpcException_Preserved_As_InnerException_BusinessException()
    {
        var client = new TestGrpcClient(BuildServiceProvider());
        var rpcEx = new RpcException(new Status(StatusCode.NotFound, "sku not found"));

        var act = async () => await client.RunExecuteAsync("op", _ => Task.FromException<int>(rpcEx));

        var thrown = (await act.Should().ThrowAsync<AntiCorruptionException>()).Which;
        thrown.InnerException.Should().BeSameAs(rpcEx);
        thrown.ErrorCode.Should().Be("TEST_SERVICE_REMOTE_FAILED");
    }

    [Fact]
    public async Task UserCancellation_Propagates_WithoutWrapping()
    {
        var client = new TestGrpcClient(BuildServiceProvider());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await client.RunExecuteAsync("op", ct => Task.FromException<int>(new OperationCanceledException(ct)), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ExecuteAsync_TransientFailure_RetriesAndSucceeds()
    {
        var sp = BuildServiceProvider(retryCount: 2);
        var client = new TestGrpcClient(sp);
        var rpcEx = new RpcException(new Status(StatusCode.Unavailable, "connection refused"));

        var callCount = 0;
        var act = async () => await client.RunExecuteAsync("op", _ =>
        {
            callCount++;
            if (callCount < 3)
            {
                return Task.FromException<int>(rpcEx);
            }
            return Task.FromResult(42);
        });

        var result = await act();
        result.Should().Be(42);
        callCount.Should().Be(3, "首次失败 + 2 次重试 = 3 次调用");
    }

    [Fact]
    public async Task ExecuteAsync_NonTransientFailure_DoesNotRetry()
    {
        var sp = BuildServiceProvider(retryCount: 2);
        var client = new TestGrpcClient(sp);
        var rpcEx = new RpcException(new Status(StatusCode.InvalidArgument, "bad request"));

        var callCount = 0;
        var act = async () => await client.RunExecuteAsync("op", _ =>
        {
            callCount++;
            return Task.FromException<int>(rpcEx);
        });

        var thrown = (await act.Should().ThrowAsync<AntiCorruptionException>()).Which;
        thrown.InnerException.Should().BeSameAs(rpcEx);
        thrown.ErrorCode.Should().Be("TEST_SERVICE_REMOTE_FAILED");
        callCount.Should().Be(1, "InvalidArgument 是业务错误，不应触发重试");
    }

    [Fact]
    public async Task ExecuteAsync_AllRetriesFail_ThrowsAntiCorruptionException()
    {
        var sp = BuildServiceProvider(retryCount: 2);
        var client = new TestGrpcClient(sp);
        var rpcEx = new RpcException(new Status(StatusCode.Unavailable, "connection refused"));

        var callCount = 0;
        var act = async () => await client.RunExecuteAsync("op", _ =>
        {
            callCount++;
            return Task.FromException<int>(rpcEx);
        });

        var thrown = (await act.Should().ThrowAsync<AntiCorruptionException>()).Which;
        thrown.InnerException.Should().BeSameAs(rpcEx);
        thrown.ErrorCode.Should().Be("TEST_SERVICE_UNAVAILABLE");
        callCount.Should().Be(3, "1 次初始调用 + 2 次重试均失败后抛出 AntiCorruptionException");
    }

    [Fact]
    public async Task ExecuteAsync_DomainException_DoesNotRetry()
    {
        var sp = BuildServiceProvider(retryCount: 2);
        var client = new TestGrpcClient(sp);
        var domainEx = new TestDomainException("business error");

        var callCount = 0;
        var act = async () => await client.RunExecuteAsync("op", _ =>
        {
            callCount++;
            return Task.FromException<int>(domainEx);
        });

        var thrown = await act.Should().ThrowAsync<TestDomainException>();
        thrown.Which.Should().BeSameAs(domainEx);
        callCount.Should().Be(1, "DomainException 是业务异常，不应触发重试也不应被包装");
    }

    [Fact]
    public async Task ExecuteAsync_CancellationToken_DoesNotRetry()
    {
        var sp = BuildServiceProvider(retryCount: 2);
        var client = new TestGrpcClient(sp);

        var callCount = 0;
        var act = async () => await client.RunExecuteAsync("op", _ =>
        {
            callCount++;
            return Task.FromException<int>(new OperationCanceledException());
        });

        await act.Should().ThrowAsync<AntiCorruptionException>();
        callCount.Should().Be(1, "OperationCanceledException 不应触发重试");
    }
}
