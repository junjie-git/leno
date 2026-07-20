using FluentAssertions;
using Grpc.Core;
using Leno.Infrastructure.AntiCorruption;
using Leno.SharedKernel.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Polly;
using Xunit;

namespace Leno.Infrastructure.Tests.AntiCorruption;

public class GrpcAntiCorruptionClientBaseTests
{
    private sealed class TestGrpcClient : GrpcAntiCorruptionClientBase
    {
        protected override string ServiceName => "test_service";

        public TestGrpcClient(IServiceProvider? serviceProvider = null, ILogger? logger = null)
            : base(serviceProvider, logger)
        {
        }

        public Task<T> RunExecuteAsync<T>(string operation, Func<CancellationToken, Task<T>> fn, CancellationToken ct = default)
            => ExecuteAsync(operation, fn, ct);
    }

    /// <summary>
    /// 测试用领域异常：<see cref="ExecuteAsync{T}"/> 必须对领域异常透传、不重试。
    /// </summary>
    private sealed class TestDomainException : DomainException
    {
        public TestDomainException(string message, string errorCode) : base(message, errorCode) { }
    }

    /// <summary>
    /// 构造一个注册了零延迟 gRPC retry 策略的 <see cref="IServiceProvider"/>，
    /// 用于验证 <see cref="GrpcAntiCorruptionClientBase"/> 的 Polly 重试行为。
    /// 默认 retryCount=2，与生产 <see cref="AntiCorruptionPollyExtensions.AddLenoGrpcAntiCorruptionPolly"/> 一致。
    /// </summary>
    private static IServiceProvider BuildServiceProviderWithFastRetry(int retryCount = 2)
    {
        var services = new ServiceCollection();
        var fastPolicy = Policy
            .Handle<RpcException>(ex => AntiCorruptionPollyExtensions.IsTransientGrpcStatus(ex.StatusCode))
            .WaitAndRetryAsync(retryCount, _ => TimeSpan.Zero);
        services.AddKeyedSingleton<IAsyncPolicy>(AntiCorruptionPollyExtensions.GrpcRetryPolicyKey, fastPolicy);
        return services.BuildServiceProvider();
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

    /// <summary>
    /// P1-B.1：临时性 gRPC 故障（Unavailable）应触发 Polly 重试，重试后成功则返回业务结果。
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_TransientFailure_RetriesAndSucceeds()
    {
        var sp = BuildServiceProviderWithFastRetry();
        var client = new TestGrpcClient(sp);
        var rpcEx = new RpcException(new Status(StatusCode.Unavailable, "down"));
        var callCount = 0;

        var result = await client.RunExecuteAsync("op", ct =>
        {
            callCount++;
            return callCount == 1
                ? Task.FromException<int>(rpcEx)
                : Task.FromResult(42);
        });

        result.Should().Be(42);
        callCount.Should().Be(2, "首次失败后应自动重试 1 次成功");
    }

    /// <summary>
    /// P1-B.1：非临时性 gRPC 故障（InvalidArgument）属于业务错误，不应触发重试。
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_NonTransientFailure_DoesNotRetry()
    {
        var sp = BuildServiceProviderWithFastRetry();
        var client = new TestGrpcClient(sp);
        var rpcEx = new RpcException(new Status(StatusCode.InvalidArgument, "bad arg"));
        var callCount = 0;

        var act = async () => await client.RunExecuteAsync("op", ct =>
        {
            callCount++;
            return Task.FromException<int>(rpcEx);
        });

        var thrown = (await act.Should().ThrowAsync<AntiCorruptionException>()).Which;
        thrown.InnerException.Should().BeSameAs(rpcEx);
        thrown.ErrorCode.Should().Be("TEST_SERVICE_REMOTE_FAILED");
        callCount.Should().Be(1, "业务错误（InvalidArgument）不应触发重试");
    }

    /// <summary>
    /// P1-B.1：临时性 gRPC 故障重试 2 次后仍失败，应抛 <see cref="AntiCorruptionException"/>
    /// 并保留 <see cref="RpcException"/> 作为 InnerException，ErrorCode 为 {SERVICE}_UNAVAILABLE。
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_AllRetriesFail_ThrowsAntiCorruptionException()
    {
        var sp = BuildServiceProviderWithFastRetry(retryCount: 2);
        var client = new TestGrpcClient(sp);
        var rpcEx = new RpcException(new Status(StatusCode.Unavailable, "down"));
        var callCount = 0;

        var act = async () => await client.RunExecuteAsync("op", ct =>
        {
            callCount++;
            return Task.FromException<int>(rpcEx);
        });

        var thrown = (await act.Should().ThrowAsync<AntiCorruptionException>()).Which;
        thrown.InnerException.Should().BeSameAs(rpcEx);
        thrown.ErrorCode.Should().Be("TEST_SERVICE_UNAVAILABLE");
        // 1 次初始调用 + 2 次重试 = 3 次
        callCount.Should().Be(3, "1 次初始 + 2 次重试后仍失败应抛 AntiCorruptionException");
    }

    /// <summary>
    /// P1-B.1：业务异常 <see cref="DomainException"/> 应透传，不重试、不包装。
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_DomainException_DoesNotRetry()
    {
        var sp = BuildServiceProviderWithFastRetry();
        var client = new TestGrpcClient(sp);
        var domainEx = new TestDomainException("biz error", "BIZ_ERROR");
        var callCount = 0;

        var act = async () => await client.RunExecuteAsync("op", ct =>
        {
            callCount++;
            return Task.FromException<int>(domainEx);
        });

        var thrown = (await act.Should().ThrowAsync<TestDomainException>()).Which;
        thrown.ErrorCode.Should().Be("BIZ_ERROR");
        callCount.Should().Be(1, "领域异常应透传、不重试");
    }

    /// <summary>
    /// P1-B.1：用户取消（<see cref="CancellationToken"/> 已取消）应直接透传
    /// <see cref="OperationCanceledException"/>，不重试、不包装为 <see cref="AntiCorruptionException"/>。
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_CancellationToken_DoesNotRetry()
    {
        var sp = BuildServiceProviderWithFastRetry();
        var client = new TestGrpcClient(sp);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var callCount = 0;

        var act = async () => await client.RunExecuteAsync("op", ct =>
        {
            callCount++;
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(42);
        }, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        callCount.Should().BeLessThanOrEqualTo(1, "用户取消不应触发重试");
    }
}
