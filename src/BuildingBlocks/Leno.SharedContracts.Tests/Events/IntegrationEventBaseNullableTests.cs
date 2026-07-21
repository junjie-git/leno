using Leno.SharedContracts.Events;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Leno.SharedContracts.Tests.Events;

/// <summary>
/// IntegrationEventBase.IdempotencyKey 可空类型修复验证。
/// T35：IdempotencyKey 从非可空 string 改为 string?，
/// 允许旧版事件 JSON 显式 null 或反序列化器配置不覆盖默认值时为 null。
/// 消费侧通过 string.IsNullOrEmpty 回退到 EventId 作为幂等键。
/// </summary>
public class IntegrationEventBaseNullableTests
{
    [Fact]
    public void IdempotencyKey_ExplicitNullInJson_ShouldDeserializeToNull()
    {
        // Arrange — JSON 显式包含 "idempotencyKey": null
        var jsonWithNull = """{"EventId":"00000000-0000-0000-0000-000000000001","OccurredAt":"2026-07-22T00:00:00Z","SchemaVersion":1,"IdempotencyKey":null}""";

        // Act
        var deserialized = JsonSerializer.Deserialize<TestEvent>(jsonWithNull)!;

        // Assert — 显式 null 应反序列化为 null（类型为 string? 允许 null）
        deserialized.IdempotencyKey.Should().BeNull(
            "JSON 显式为 null 时，string? 类型的 IdempotencyKey 应为 null");
    }

    [Fact]
    public void IdempotencyKey_DefaultValue_ShouldBeEmptyString()
    {
        // Arrange & Act — 无参构造
        var evt = new TestEvent();

        // Assert — 字段初始化器提供 string.Empty 默认值
        evt.IdempotencyKey.Should().BeEmpty("字段初始化器默认为 string.Empty");
    }

    [Fact]
    public void IdempotencyKey_OldJsonWithoutField_ShouldDeserializeToEmpty()
    {
        // Arrange — 旧版 JSON 无 IdempotencyKey 字段
        var oldJson = """{"EventId":"00000000-0000-0000-0000-000000000002","OccurredAt":"2026-07-22T00:00:00Z","SchemaVersion":1}""";

        // Act
        var deserialized = JsonSerializer.Deserialize<TestEvent>(oldJson)!;

        // Assert — 缺字段时字段初始化器提供 string.Empty
        deserialized.IdempotencyKey.Should().BeEmpty(
            "旧版 JSON 缺 IdempotencyKey 字段时应保持字段初始化器的 string.Empty");
    }

    [Fact]
    public void IdempotencyKey_CanBeAssignedNull()
    {
        // Arrange & Act — 验证 string? 类型允许赋值 null
        TestEvent evt = new() { IdempotencyKey = null };

        // Assert
        evt.IdempotencyKey.Should().BeNull("string? 类型允许 null 赋值");
    }

    [Fact]
    public void IdempotencyKey_EmptyOrNull_ConsumerShouldFallbackToEventId()
    {
        // Arrange — 模拟消费侧逻辑：string.IsNullOrEmpty 时回退到 EventId
        var evtWithNull = new TestEvent { IdempotencyKey = null };
        var evtWithEmpty = new TestEvent { IdempotencyKey = string.Empty };
        var evtWithKey = new TestEvent { IdempotencyKey = "custom-key-123" };

        // Act — 模拟 IntegrationEventConsumerBase 中的 effectiveKey 逻辑
        var effectiveKeyNull = string.IsNullOrEmpty(evtWithNull.IdempotencyKey)
            ? evtWithNull.EventId.ToString()
            : evtWithNull.IdempotencyKey;
        var effectiveKeyEmpty = string.IsNullOrEmpty(evtWithEmpty.IdempotencyKey)
            ? evtWithEmpty.EventId.ToString()
            : evtWithEmpty.IdempotencyKey;
        var effectiveKeyCustom = string.IsNullOrEmpty(evtWithKey.IdempotencyKey)
            ? evtWithKey.EventId.ToString()
            : evtWithKey.IdempotencyKey;

        // Assert
        effectiveKeyNull.Should().Be(evtWithNull.EventId.ToString(),
            "null IdempotencyKey 应回退到 EventId");
        effectiveKeyEmpty.Should().Be(evtWithEmpty.EventId.ToString(),
            "空字符串 IdempotencyKey 应回退到 EventId");
        effectiveKeyCustom.Should().Be("custom-key-123",
            "非空 IdempotencyKey 应使用原值");
    }

    [Fact]
    public void IdempotencyKey_ConstructorWithNull_ShouldFallbackToEventId()
    {
        // Arrange & Act — 显式构造传入 null idempotencyKey
        var eventId = Guid.NewGuid();
        var evt = new TestEventWithConstructor(eventId, DateTime.UtcNow, null);

        // Assert — null 时回退到 EventId.ToString()
        evt.IdempotencyKey.Should().Be(eventId.ToString(),
            "构造函数传入 null idempotencyKey 应回退到 EventId.ToString()");
    }

    [Fact]
    public void IdempotencyKey_ConstructorWithValidKey_ShouldUseProvidedKey()
    {
        // Arrange & Act
        var evt = new TestEventWithConstructor(Guid.NewGuid(), DateTime.UtcNow, "explicit-key");

        // Assert
        evt.IdempotencyKey.Should().Be("explicit-key");
    }

    private sealed class TestEvent : IntegrationEventBase
    {
        public Guid AggregateId => EventId;
    }

    private sealed class TestEventWithConstructor : IntegrationEventBase
    {
        public TestEventWithConstructor(Guid? eventId, DateTime? occurredAt, string? idempotencyKey)
            : base(eventId, occurredAt, idempotencyKey)
        {
        }
    }
}
