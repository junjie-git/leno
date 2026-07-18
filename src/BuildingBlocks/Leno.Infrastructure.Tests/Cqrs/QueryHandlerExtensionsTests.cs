using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Leno.Infrastructure.Abstractions.Cqrs;
using Leno.Infrastructure.Cqrs;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Leno.Infrastructure.Tests.Cqrs;

public class QueryHandlerExtensionsTests
{
    public sealed class FakeQuery { public string Keyword { get; init; } = string.Empty; }
    public sealed class FakeResult { public string Value { get; init; } = string.Empty; }

    public sealed class FakeQueryHandler : IQueryHandler<FakeQuery, FakeResult>
    {
        public Task<FakeResult> HandleAsync(FakeQuery query, CancellationToken ct = default)
            => Task.FromResult(new FakeResult { Value = $"Hello {query.Keyword}" });
    }

    public sealed class MultiInterfaceQueryHandler
        : IQueryHandler<MultiQuery, MultiResult>
        , IQueryHandler<AnotherQuery, AnotherResult>
    {
        public Task<MultiResult> HandleAsync(MultiQuery query, CancellationToken ct = default)
            => Task.FromResult(new MultiResult { Value = "first" });

        public Task<AnotherResult> HandleAsync(AnotherQuery query, CancellationToken ct = default)
            => Task.FromResult(new AnotherResult { Data = "second" });
    }

    public sealed class MultiQuery { }
    public sealed class MultiResult { public string Value { get; init; } = string.Empty; }
    public sealed class AnotherQuery { }
    public sealed class AnotherResult { public string Data { get; init; } = string.Empty; }

    [Fact]
    public void AddQueryHandlers_RegistersSingleInterfaceHandler()
    {
        var services = new ServiceCollection();
        services.AddQueryHandlers(typeof(FakeQueryHandler).Assembly);

        // 验证注册（扫描整个程序集可能注册多个 handler，使用 FirstOrDefault 配合 ImplementationType 过滤更稳妥）
        var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IQueryHandler<FakeQuery, FakeResult>)
            && s.ImplementationType == typeof(FakeQueryHandler));
        descriptor.Should().NotBeNull();
        descriptor!.ImplementationType.Should().Be(typeof(FakeQueryHandler));
        descriptor.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddQueryHandlers_RegistersAllImplementedInterfaces()
    {
        var services = new ServiceCollection();
        services.AddQueryHandlers(typeof(MultiInterfaceQueryHandler).Assembly);

        var d1 = services.FirstOrDefault(s => s.ServiceType == typeof(IQueryHandler<MultiQuery, MultiResult>)
            && s.ImplementationType == typeof(MultiInterfaceQueryHandler));
        var d2 = services.FirstOrDefault(s => s.ServiceType == typeof(IQueryHandler<AnotherQuery, AnotherResult>)
            && s.ImplementationType == typeof(MultiInterfaceQueryHandler));

        d1.Should().NotBeNull();
        d2.Should().NotBeNull();
    }

    [Fact]
    public void AddQueryHandlers_WithSingletonLifetime_RegistersAsSingleton()
    {
        var services = new ServiceCollection();
        services.AddQueryHandlers(typeof(FakeQueryHandler).Assembly, ServiceLifetime.Singleton);

        var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IQueryHandler<FakeQuery, FakeResult>)
            && s.ImplementationType == typeof(FakeQueryHandler));
        descriptor.Should().NotBeNull();
        descriptor!.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddQueryHandlers_CanResolveAndInvokeHandler()
    {
        var services = new ServiceCollection();
        services.AddQueryHandlers(typeof(FakeQueryHandler).Assembly);

        var provider = services.BuildServiceProvider();
        var handler = provider.GetRequiredService<IQueryHandler<FakeQuery, FakeResult>>();
        var result = handler.HandleAsync(new FakeQuery { Keyword = "World" }).GetAwaiter().GetResult();

        result.Value.Should().Be("Hello World");
    }
}
