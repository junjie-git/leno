using FluentAssertions;
using Grpc.Core;
using Leno.Infrastructure.AntiCorruption;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Leno.Infrastructure.Tests.AntiCorruption;

/// <summary>
/// 泛型防腐层调度器（单轨 gRPC + 熔断 fail-fast）测试。
/// 双轨下线后：仅持有一个 gRPC 实现，熔断 Open 时 fail-fast，无 HTTP 降级路径。
/// 传输类故障（Unavailable/DeadlineExceeded/Internal/ResourceExhausted）计熔断；
/// 业务类失败不计熔断、原样上抛。
/// </summary>
public class AntiCorruptionDispatcherTests
{
    private sealed class FakeService
    {
        public int Calls;

        public Task<string> OkAsync()
        {
            Calls++;
            return Task.FromResult("ok");
        }

        public Task<string> FailAsync()
        {
            Calls++;
            // 模拟 gRPC 传输层故障：RpcException(Unavailable) 包装为 AntiCorruptionException
            throw new AntiCorruptionException(
                "gRPC 不可用",
                new RpcException(new Status(StatusCode.Unavailable, "connection refused")),
                "grpc_unavailable");
        }

        public Task<string> BizFailAsync()
        {
            Calls++;
            throw new AntiCorruptionException("业务不存在", "not_found");
        }
    }

    private static (AntiCorruptionDispatcher<FakeService> Dispatcher, FakeService Impl, CircuitBreakerState CircuitBreaker) Create()
    {
        var impl = new FakeService();
        // 与 Order DI 现文同构：new CircuitBreakerState(name, failureThreshold, successThreshold, openDuration)
        var cb = new CircuitBreakerState("test", 3, 2, TimeSpan.FromSeconds(30));
        var dispatcher = new AntiCorruptionDispatcher<FakeService>(
            impl,
            NullLogger<AntiCorruptionDispatcher<FakeService>>.Instance,
            "test",
            cb);
        return (dispatcher, impl, cb);
    }

    [Fact]
    public async Task ExecuteAsync_Success_RecordsCircuitSuccess()
    {
        var (dispatcher, impl, _) = Create();

        var result = await dispatcher.ExecuteAsync(s => s.OkAsync());

        result.Should().Be("ok");
        impl.Calls.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_BusinessFailure_PropagatesWithoutRetry()
    {
        var (dispatcher, impl, _) = Create();

        var act = async () => await dispatcher.ExecuteAsync(s => s.BizFailAsync());

        await act.Should().ThrowAsync<AntiCorruptionException>();
        impl.Calls.Should().Be(1); // 业务失败不重试，异常原样上抛
    }

    [Fact]
    public async Task ExecuteAsync_BusinessFailures_DoNotTripCircuit()
    {
        var (dispatcher, impl, cb) = Create();

        // 连续 5 次业务失败（超过 failureThreshold=3）——业务类失败不计熔断
        for (var i = 0; i < 5; i++)
        {
            var act = async () => await dispatcher.ExecuteAsync(s => s.BizFailAsync());
            await act.Should().ThrowAsync<AntiCorruptionException>().WithMessage("*业务不存在*");
        }

        cb.GetState().Should().NotBe(CircuitState.Open);
        impl.Calls.Should().Be(5); // 每次都真实触达实现，从未 fail-fast
    }

    [Fact]
    public async Task ExecuteAsync_OpenCircuit_FailsFast_WithoutCallingImplementation()
    {
        var (dispatcher, impl, cb) = Create();

        for (var i = 0; i < 3; i++)
        {
            var act = async () => await dispatcher.ExecuteAsync(s => s.FailAsync());
            await act.Should().ThrowAsync<AntiCorruptionException>();
        }

        cb.GetState().Should().Be(CircuitState.Open);

        var callsBefore = impl.Calls;
        var actAfterOpen = async () => await dispatcher.ExecuteAsync(s => s.OkAsync());
        (await actAfterOpen.Should().ThrowAsync<AntiCorruptionException>())
            .Which.Message.Should().Contain("熔断 Open");
        impl.Calls.Should().Be(callsBefore); // fail-fast：Open 期间不再触达实现
    }
}
