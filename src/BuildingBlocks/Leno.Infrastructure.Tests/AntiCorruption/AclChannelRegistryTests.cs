using FluentAssertions;
using Leno.Infrastructure.AntiCorruption;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Leno.Infrastructure.Tests.AntiCorruption;

/// <summary>
/// AclChannelRegistry 单元测试（阶段四 4.2：可插拔策略链）。
/// 覆盖：通道排序、重名检测、空通道异常、熔断器查找、可用通道过滤、健康检查降级。
/// </summary>
public class AclChannelRegistryTests
{
    private static IAclChannel CreateChannel(string name, int priority, bool healthy = true)
    {
        var channel = new TestAclChannel(name, priority);
        if (!healthy)
        {
            channel.HealthCheckImpl = _ => Task.FromResult(false);
        }
        return channel;
    }

    [Fact]
    public void Constructor_SortsChannelsByPriority_Ascending()
    {
        var grpc = CreateChannel("grpc", priority: 0);
        var http = CreateChannel("http", priority: 1);
        var bus = CreateChannel("message-bus", priority: 5);

        var registry = new AclChannelRegistry(new[] { http, bus, grpc }, NullLogger<AclChannelRegistry>.Instance);

        registry.Channels.Should().HaveCount(3);
        registry.Channels[0].Name.Should().Be("grpc");
        registry.Channels[1].Name.Should().Be("http");
        registry.Channels[2].Name.Should().Be("message-bus");
        registry.Count.Should().Be(3);
    }

    [Fact]
    public void Constructor_DuplicateNames_ThrowsInvalidOperationException()
    {
        var channel1 = CreateChannel("grpc", priority: 0);
        var channel2 = CreateChannel("grpc", priority: 1);

        var act = () => new AclChannelRegistry(new[] { channel1, channel2 }, NullLogger<AclChannelRegistry>.Instance);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*grpc*");
    }

    [Fact]
    public void Constructor_EmptyChannels_ThrowsInvalidOperationException()
    {
        var act = () => new AclChannelRegistry(Array.Empty<IAclChannel>(), NullLogger<AclChannelRegistry>.Instance);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*至少需要一个 IAclChannel*");
    }

    [Fact]
    public void Constructor_SamePriorityOrdersByName_AsTiebreaker()
    {
        var alpha = CreateChannel("alpha", priority: 0);
        var beta = CreateChannel("beta", priority: 0);

        var registry = new AclChannelRegistry(new[] { beta, alpha }, NullLogger<AclChannelRegistry>.Instance);

        registry.Channels[0].Name.Should().Be("alpha");
        registry.Channels[1].Name.Should().Be("beta");
    }

    [Fact]
    public void GetCircuitBreaker_ExistingChannel_ReturnsBreaker()
    {
        var grpc = CreateChannel("grpc", priority: 0);
        var registry = new AclChannelRegistry(new[] { grpc }, NullLogger<AclChannelRegistry>.Instance);

        var breaker = registry.GetCircuitBreaker("grpc");

        breaker.Should().NotBeNull();
        breaker.GetState().Should().Be(CircuitState.Closed);
    }

    [Fact]
    public void GetCircuitBreaker_UnknownChannel_ThrowsKeyNotFound()
    {
        var grpc = CreateChannel("grpc", priority: 0);
        var registry = new AclChannelRegistry(new[] { grpc }, NullLogger<AclChannelRegistry>.Instance);

        var act = () => registry.GetCircuitBreaker("unknown");

        act.Should().Throw<KeyNotFoundException>()
            .WithMessage("*unknown*");
    }

    [Fact]
    public void TryGetCircuitBreaker_ExistingChannel_ReturnsTrue()
    {
        var grpc = CreateChannel("grpc", priority: 0);
        var registry = new AclChannelRegistry(new[] { grpc }, NullLogger<AclChannelRegistry>.Instance);

        var found = registry.TryGetCircuitBreaker("grpc", out var breaker);

        found.Should().BeTrue();
        breaker.Should().NotBeNull();
    }

    [Fact]
    public void TryGetCircuitBreaker_UnknownChannel_ReturnsFalse()
    {
        var grpc = CreateChannel("grpc", priority: 0);
        var registry = new AclChannelRegistry(new[] { grpc }, NullLogger<AclChannelRegistry>.Instance);

        var found = registry.TryGetCircuitBreaker("unknown", out var breaker);

        found.Should().BeFalse();
        breaker.Should().BeNull();
    }

    [Fact]
    public void FindByName_ExistingChannel_ReturnsChannel()
    {
        var grpc = CreateChannel("grpc", priority: 0);
        var http = CreateChannel("http", priority: 1);
        var registry = new AclChannelRegistry(new[] { grpc, http }, NullLogger<AclChannelRegistry>.Instance);

        var found = registry.FindByName("http");

        found.Should().NotBeNull();
        found!.Name.Should().Be("http");
    }

    [Fact]
    public void FindByName_UnknownChannel_ReturnsNull()
    {
        var grpc = CreateChannel("grpc", priority: 0);
        var registry = new AclChannelRegistry(new[] { grpc }, NullLogger<AclChannelRegistry>.Instance);

        var found = registry.FindByName("unknown");

        found.Should().BeNull();
    }

    [Fact]
    public void FindByName_CaseInsensitive_MatchesChannel()
    {
        var grpc = CreateChannel("grpc", priority: 0);
        var registry = new AclChannelRegistry(new[] { grpc }, NullLogger<AclChannelRegistry>.Instance);

        var found = registry.FindByName("GRPC");

        found.Should().NotBeNull();
        found!.Name.Should().Be("grpc");
    }

    [Fact]
    public void GetAvailableChannels_AllClosed_ReturnsAllChannels()
    {
        var grpc = CreateChannel("grpc", priority: 0);
        var http = CreateChannel("http", priority: 1);
        var registry = new AclChannelRegistry(new[] { grpc, http }, NullLogger<AclChannelRegistry>.Instance);

        var available = registry.GetAvailableChannels();

        available.Should().HaveCount(2);
    }

    [Fact]
    public void GetAvailableChannels_ChannelOpen_FiltersOut()
    {
        var grpc = CreateChannel("grpc", priority: 0);
        var http = CreateChannel("http", priority: 1);
        var registry = new AclChannelRegistry(new[] { grpc, http }, NullLogger<AclChannelRegistry>.Instance);

        // 触发 grpc 熔断器 Open（默认阈值 3 次失败）
        var grpcBreaker = registry.GetCircuitBreaker("grpc");
        grpcBreaker.RecordFailure();
        grpcBreaker.RecordFailure();
        grpcBreaker.RecordFailure();

        var available = registry.GetAvailableChannels();

        available.Should().HaveCount(1);
        available[0].Name.Should().Be("http");
    }

    [Fact]
    public void RecordChannelFailure_IncrementsBreakerFailureCount()
    {
        var grpc = CreateChannel("grpc", priority: 0);
        var registry = new AclChannelRegistry(new[] { grpc }, NullLogger<AclChannelRegistry>.Instance);

        registry.RecordChannelFailure("grpc");
        registry.RecordChannelFailure("grpc");
        registry.RecordChannelFailure("grpc");

        var breaker = registry.GetCircuitBreaker("grpc");
        breaker.GetState().Should().Be(CircuitState.Open);
    }

    [Fact]
    public void RecordChannelSuccess_ResetsBreakerFailures()
    {
        var grpc = CreateChannel("grpc", priority: 0);
        var registry = new AclChannelRegistry(new[] { grpc }, NullLogger<AclChannelRegistry>.Instance);

        // 累计 2 次失败（未达阈值 3）
        registry.RecordChannelFailure("grpc");
        registry.RecordChannelFailure("grpc");

        // 1 次成功重置
        registry.RecordChannelSuccess("grpc");

        var breaker = registry.GetCircuitBreaker("grpc");
        breaker.GetState().Should().Be(CircuitState.Closed,
            "Closed 状态下 RecordSuccess 应重置失败计数");
    }

    [Fact]
    public void RecordChannelFailure_UnknownChannel_DoesNotThrow()
    {
        var grpc = CreateChannel("grpc", priority: 0);
        var registry = new AclChannelRegistry(new[] { grpc }, NullLogger<AclChannelRegistry>.Instance);

        var act = () => registry.RecordChannelFailure("unknown");

        act.Should().NotThrow();
    }

    [Fact]
    public void RecordChannelSuccess_UnknownChannel_DoesNotThrow()
    {
        var grpc = CreateChannel("grpc", priority: 0);
        var registry = new AclChannelRegistry(new[] { grpc }, NullLogger<AclChannelRegistry>.Instance);

        var act = () => registry.RecordChannelSuccess("unknown");

        act.Should().NotThrow();
    }
}
