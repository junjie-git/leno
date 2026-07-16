using Leno.Infrastructure.Outbox;
using Leno.SharedContracts.Events;

namespace Leno.Infrastructure.Tests.Outbox;

/// <summary>
/// T22.3: IOutboxEventTypeResolver 测试——按 FullName 解析，兼容 BC 版本升级（程序集版本变更/命名空间迁移）。
/// </summary>
public class OutboxEventTypeResolverTests
{
    private readonly DefaultOutboxEventTypeResolver _resolver = DefaultOutboxEventTypeResolver.Instance;

    /// <summary>
    /// 测试用集成事件类型，位于测试程序集内。
    /// </summary>
    private sealed class TestIntegrationEvent : IntegrationEventBase
    {
        public string Content { get; init; } = string.Empty;
    }

    [Fact]
    public void Resolve_ByFullName_ShouldReturnType()
    {
        // Arrange：OutboxMessage.Create 现在存储 FullName
        var evt = new TestIntegrationEvent { Content = "test" };
        var message = OutboxMessage.Create(evt);

        // Act
        var resolved = _resolver.Resolve(message.Type);

        // Assert
        resolved.Should().NotBeNull();
        resolved.Should().Be<TestIntegrationEvent>();
        // Type 字段应为 FullName（不含程序集信息），跨版本更稳定
        message.Type.Should().Be(typeof(TestIntegrationEvent).FullName);
    }

    [Fact]
    public void Resolve_ByAssemblyQualifiedName_ShouldAlsoWork_ForBackwardCompatibility()
    {
        // Arrange：历史数据存储的是 AssemblyQualifiedName（T22.3 之前的格式）
        var assemblyQualifiedName = typeof(TestIntegrationEvent).AssemblyQualifiedName!;

        // Act
        var resolved = _resolver.Resolve(assemblyQualifiedName);

        // Assert：resolver 应能从 AssemblyQualifiedName 中提取 FullName 并解析
        resolved.Should().NotBeNull();
        resolved.Should().Be<TestIntegrationEvent>();
    }

    [Fact]
    public void Resolve_UnknownType_ShouldReturnNull()
    {
        // Act
        var resolved = _resolver.Resolve("Nonexistent.Namespace.NonexistentType, NonexistentAssembly");

        // Assert
        resolved.Should().BeNull();
    }

    [Fact]
    public void Resolve_EmptyOrNull_ShouldReturnNull()
    {
        _resolver.Resolve("").Should().BeNull();
        _resolver.Resolve("   ").Should().BeNull();
    }

    [Fact]
    public void Resolve_ShouldCacheResults()
    {
        // Arrange
        var fullName = typeof(TestIntegrationEvent).FullName!;

        // Act：多次调用应返回同一 Type 实例（缓存生效）
        var first = _resolver.Resolve(fullName);
        var second = _resolver.Resolve(fullName);

        // Assert
        first.Should().BeSameAs(second);
        first.Should().Be<TestIntegrationEvent>();
    }

    /// <summary>
    /// 模拟 BC 版本升级场景：历史消息的 AssemblyQualifiedName 含旧版本号，
    /// resolver 提取 FullName 后在当前已加载程序集中查找，兼容版本变更。
    /// </summary>
    [Fact]
    public void Resolve_WithStaleVersionInAssemblyQualifiedName_ShouldResolveByFullName()
    {
        // Arrange：构造一个版本号过期的 AssemblyQualifiedName
        // 形如 "Namespace.Type, Assembly, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null"
        // 但程序集当前版本可能是 2.0.0.0，Type.GetType 会失败，resolver 需按 FullName 查找
        var fullName = typeof(TestIntegrationEvent).FullName!;
        var assemblyName = typeof(TestIntegrationEvent).Assembly.GetName().Name!;
        var staleAqn = $"{fullName}, {assemblyName}, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";

        // Act
        var resolved = _resolver.Resolve(staleAqn);

        // Assert：即使版本号不匹配，按 FullName 仍能解析到类型
        resolved.Should().NotBeNull();
        resolved.Should().Be<TestIntegrationEvent>();
    }

    /// <summary>
    /// 自定义 resolver 实现可注入 OutboxPublisher 替换默认行为，
    /// 用于处理命名空间迁移等更复杂的类型映射。
    /// </summary>
    [Fact]
    public void CustomResolver_CanBeInjectedIntoPublisher()
    {
        // Arrange：自定义 resolver 将 "OldNamespace.OldEvent" 映射到 TestIntegrationEvent
        var customResolver = new CustomResolverForTest();

        // Act & Assert：resolver 返回自定义映射的类型
        var resolved = customResolver.Resolve("OldNamespace.OldEvent");
        resolved.Should().Be<TestIntegrationEvent>();
    }

    private sealed class CustomResolverForTest : IOutboxEventTypeResolver
    {
        public Type? Resolve(string typeName)
        {
            if (typeName == "OldNamespace.OldEvent")
            {
                return typeof(TestIntegrationEvent);
            }
            return DefaultOutboxEventTypeResolver.Instance.Resolve(typeName);
        }
    }
}
