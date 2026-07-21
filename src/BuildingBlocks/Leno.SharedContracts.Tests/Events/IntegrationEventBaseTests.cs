// 文件：src/BuildingBlocks/Leno.SharedContracts.Tests/Events/IntegrationEventBaseTests.cs
using Leno.SharedContracts.Events;
using Xunit;
using FluentAssertions;
using System.Text.Json;

namespace Leno.SharedContracts.Tests.Events;

public class IntegrationEventBaseTests
{
    [Fact]
    public void IdempotencyKey_DefaultValue_ShouldBeEmptyStringNotNull()
    {
        // Arrange & Act — 用无参构造创建子类实例
        var evt = new TestEvent();

        // Assert — 默认值应为 string.Empty 而非 null
        evt.IdempotencyKey.Should().NotBeNull("IdempotencyKey 不应为 null");
        evt.IdempotencyKey.Should().BeEmpty("无参构造时 IdempotencyKey 应为空字符串（由字段初始化器提供）");
    }

    [Fact]
    public void IdempotencyKey_OldJsonWithoutField_ShouldDeserializeToEmpty()
    {
        // Arrange — 旧版 JSON 无 IdempotencyKey 字段
        var oldJson = """{"EventId":"00000000-0000-0000-0000-000000000001","OccurredAt":"2026-07-22T00:00:00Z","SchemaVersion":1}""";

        // Act
        var deserialized = JsonSerializer.Deserialize<TestEvent>(oldJson)!;

        // Assert — 旧版 JSON 缺字段时反序列化为空字符串而非 null
        deserialized.IdempotencyKey.Should().NotBeNull("反序列化后 IdempotencyKey 不应为 null");
        deserialized.IdempotencyKey.Should().BeEmpty("旧版事件缺 IdempotencyKey 字段时应反序列化为空字符串");
    }

    [Fact]
    public void EventId_DefaultValue_ShouldBeNewGuid()
    {
        // Arrange & Act
        var evt = new TestEvent();

        // Assert — EventId 应为新生成的 Guid，不应为 Guid.Empty
        evt.EventId.Should().NotBeEmpty("EventId 应为新生成的 Guid");
    }

    private sealed class TestEvent : IntegrationEventBase
    {
        public Guid AggregateId => EventId;
    }
}
