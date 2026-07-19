using FluentAssertions;
using Grpc.Core;
using Leno.Infrastructure.AntiCorruption;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Leno.Infrastructure.Tests.AntiCorruption;

public class AntiCorruptionDispatcherTests
{
    public interface ITestService
    {
        Task<string> GetValueAsync(CancellationToken ct);
    }

    private sealed class HttpImpl : ITestService
    {
        public int CallCount;
        public Func<string>? ReturnValue { get; set; }
        public Exception? Throw { get; set; }
        public Task<string> GetValueAsync(CancellationToken ct)
        {
            CallCount++;
            if (Throw is not null) return Task.FromException<string>(Throw);
            return Task.FromResult(ReturnValue?.Invoke() ?? "http-value");
        }
    }

    private sealed class GrpcImpl : ITestService
    {
        public int CallCount;
        public Func<string>? ReturnValue { get; set; }
        public Exception? Throw { get; set; }
        public Task<string> GetValueAsync(CancellationToken ct)
        {
            CallCount++;
            if (Throw is not null) return Task.FromException<string>(Throw);
            return Task.FromResult(ReturnValue?.Invoke() ?? "grpc-value");
        }
    }

    private static IOptionsMonitor<AntiCorruptionOptions> CreateOptionsMonitor(bool useGrpc)
    {
        var mock = new Mock<IOptionsMonitor<AntiCorruptionOptions>>();
        mock.SetupGet(o => o.CurrentValue).Returns(new AntiCorruptionOptions { UseGrpc = useGrpc });
        return mock.Object;
    }

    private static AntiCorruptionDispatcher<ITestService> CreateDispatcher(
        HttpImpl http,
        GrpcImpl? grpc,
        bool useGrpc,
        CircuitBreakerState? cb = null,
        string serviceName = "test")
    {
        cb ??= new CircuitBreakerState(serviceName, 3, 2, TimeSpan.FromSeconds(30));
        return new AntiCorruptionDispatcher<ITestService>(
            http, grpc, CreateOptionsMonitor(useGrpc),
            NullLogger<AntiCorruptionDispatcher<ITestService>>.Instance,
            serviceName, cb);
    }

    [Fact]
    public async Task UseGrpc_False_AlwaysCallsHttp()
    {
        var http = new HttpImpl();
        var grpc = new GrpcImpl();
        var dispatcher = CreateDispatcher(http, grpc, useGrpc: false);

        var result = await dispatcher.ExecuteAsync(s => s.GetValueAsync(default));

        result.Should().Be("http-value");
        http.CallCount.Should().Be(1);
        grpc.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task UseGrpc_True_Closed_CallsGrpc()
    {
        var http = new HttpImpl();
        var grpc = new GrpcImpl();
        var dispatcher = CreateDispatcher(http, grpc, useGrpc: true);

        var result = await dispatcher.ExecuteAsync(s => s.GetValueAsync(default));

        result.Should().Be("grpc-value");
        grpc.CallCount.Should().Be(1);
        http.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task UseGrpc_True_Open_FallsBackToHttp()
    {
        var http = new HttpImpl();
        var grpc = new GrpcImpl();
        var cb = new CircuitBreakerState("test", 1, 2, TimeSpan.FromSeconds(30));
        cb.RecordFailure();  // 触发 Open
        var dispatcher = CreateDispatcher(http, grpc, useGrpc: true, cb: cb);

        var result = await dispatcher.ExecuteAsync(s => s.GetValueAsync(default));

        result.Should().Be("http-value");
        http.CallCount.Should().Be(1);
        grpc.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task UseGrpc_True_GrpcUnavailable_FallsBackToHttp()
    {
        var http = new HttpImpl();
        var grpc = new GrpcImpl { Throw = new AntiCorruptionException("grpc failed",
            new RpcException(new Status(StatusCode.Unavailable, "down")), "TEST_UNAVAILABLE") };
        var dispatcher = CreateDispatcher(http, grpc, useGrpc: true);

        var result = await dispatcher.ExecuteAsync(s => s.GetValueAsync(default));

        result.Should().Be("http-value");
        grpc.CallCount.Should().Be(1);
        http.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task UseGrpc_True_GrpcNotFound_DoesNotFallback()
    {
        var http = new HttpImpl();
        var grpc = new GrpcImpl { Throw = new AntiCorruptionException("not found",
            new RpcException(new Status(StatusCode.NotFound, "missing")), "TEST_REMOTE_FAILED") };
        var dispatcher = CreateDispatcher(http, grpc, useGrpc: true);

        var act = async () => await dispatcher.ExecuteAsync(s => s.GetValueAsync(default));

        await act.Should().ThrowAsync<AntiCorruptionException>();
        grpc.CallCount.Should().Be(1);
        http.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task GrpcFailure_ReachesThreshold_ThrowsAfterFallback()
    {
        var http = new HttpImpl();
        var grpc = new GrpcImpl { Throw = new AntiCorruptionException("grpc failed",
            new RpcException(new Status(StatusCode.Unavailable, "down")), "TEST_UNAVAILABLE") };
        var cb = new CircuitBreakerState("test", 1, 2, TimeSpan.FromSeconds(30));  // 阈值 1，第一次失败即 Open
        var dispatcher = CreateDispatcher(http, grpc, useGrpc: true, cb: cb);

        // 第一次失败 → 熔断 Open → 本次抛（不降级）
        var act = async () => await dispatcher.ExecuteAsync(s => s.GetValueAsync(default));
        await act.Should().ThrowAsync<AntiCorruptionException>();

        grpc.CallCount.Should().Be(1);
        http.CallCount.Should().Be(0);  // 熔断 Open 后不降级直接抛
    }

    [Fact]
    public async Task HalfOpen_ProbeSuccess_ClosesCircuit()
    {
        var http = new HttpImpl();
        var grpc = new GrpcImpl();
        var cb = new CircuitBreakerState("test", 1, 2, TimeSpan.FromSeconds(1));
        cb.RecordFailure();  // Open
        Thread.Sleep(1100);  // 转 HalfOpen
        var dispatcher = CreateDispatcher(http, grpc, useGrpc: true, cb: cb);

        await dispatcher.ExecuteAsync(s => s.GetValueAsync(default));
        await dispatcher.ExecuteAsync(s => s.GetValueAsync(default));  // 2 次成功

        cb.GetState().Should().Be(CircuitState.Closed);
    }

    [Fact]
    public async Task HalfOpen_ProbeFailure_ReopensCircuit()
    {
        var http = new HttpImpl();
        var grpc = new GrpcImpl { Throw = new AntiCorruptionException("grpc failed",
            new RpcException(new Status(StatusCode.Unavailable, "down")), "TEST_UNAVAILABLE") };
        var cb = new CircuitBreakerState("test", 1, 2, TimeSpan.FromSeconds(1));
        cb.RecordFailure();
        Thread.Sleep(1100);  // HalfOpen
        var dispatcher = CreateDispatcher(http, grpc, useGrpc: true, cb: cb);

        // HalfOpen 探测失败 → 重开 Open → 不降级（HalfOpen 失败也算熔断）
        // 但 cb 阈值 1，第一次失败即 Open，所以本次抛
        var act = async () => await dispatcher.ExecuteAsync(s => s.GetValueAsync(default));
        await act.Should().ThrowAsync<AntiCorruptionException>();

        cb.GetState().Should().Be(CircuitState.Open);
    }

    [Fact]
    public async Task GrpcImpl_Null_FallsBackToHttp()
    {
        var http = new HttpImpl();
        var dispatcher = CreateDispatcher(http, grpc: null, useGrpc: true);

        var result = await dispatcher.ExecuteAsync(s => s.GetValueAsync(default));

        result.Should().Be("http-value");
        http.CallCount.Should().Be(1);
    }
}
