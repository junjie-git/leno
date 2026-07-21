// 文件：src/BuildingBlocks/Leno.SharedContracts.Tests/Events/RefundCompletedEventTests.cs
using Leno.SharedContracts.Events;
using Xunit;
using FluentAssertions;
using System.Text.Json;

namespace Leno.SharedContracts.Tests.Events;

public class RefundCompletedEventTests
{
    [Fact]
    public void RefundCompletedEvent_ShouldHaveChannelRefundNoField()
    {
        // Arrange
        var evt = new RefundCompletedEvent(
            orderId: Guid.NewGuid(),
            userId: Guid.NewGuid(),
            refundId: Guid.NewGuid(),
            afterSalesId: Guid.NewGuid(),
            refundAmount: 100m,
            currency: "CNY",
            completedAt: DateTime.UtcNow);

        // Act
        var channelRefundNo = evt.ChannelRefundNo;

        // Assert — 字段存在且默认为 string.Empty（向后兼容）
        channelRefundNo.Should().BeEmpty("ChannelRefundNo 默认为空字符串以保持向后兼容");
    }

    [Fact]
    public void RefundCompletedEvent_WithChannelRefundNo_ShouldRoundTripThroughJson()
    {
        // Arrange
        var originalChannelRefundNo = "4200_2026072200001";
        var evt = new RefundCompletedEvent(
            orderId: Guid.NewGuid(),
            userId: Guid.NewGuid(),
            refundId: Guid.NewGuid(),
            afterSalesId: Guid.NewGuid(),
            refundAmount: 88.5m,
            currency: "CNY",
            completedAt: DateTime.UtcNow,
            channelRefundNo: originalChannelRefundNo);

        // Act
        var json = JsonSerializer.Serialize(evt);
        var deserialized = JsonSerializer.Deserialize<RefundCompletedEvent>(json)!;

        // Assert
        deserialized.ChannelRefundNo.Should().Be(originalChannelRefundNo,
            "ChannelRefundNo 应通过 JSON 序列化/反序列化保留");
    }

    [Fact]
    public void RefundCompletedEvent_OldJsonWithoutChannelRefundNo_ShouldDeserializeToEmpty()
    {
        // Arrange — 旧版事件 JSON 无 ChannelRefundNo 字段
        var oldJson = """{"OrderId":"00000000-0000-0000-0000-000000000001","UserId":"00000000-0000-0000-0000-000000000002","RefundId":"00000000-0000-0000-0000-000000000003","RefundAmount":50.0,"Currency":"CNY","CompletedAt":"2026-07-22T00:00:00Z","AfterSalesId":"00000000-0000-0000-0000-000000000004","EventId":"00000000-0000-0000-0000-000000000005","OccurredAt":"2026-07-22T00:00:00Z","IdempotencyKey":"k1","SchemaVersion":1}""";

        // Act
        var deserialized = JsonSerializer.Deserialize<RefundCompletedEvent>(oldJson)!;

        // Assert — 旧版 JSON 缺字段时反序列化为空字符串而非 null
        deserialized.ChannelRefundNo.Should().BeEmpty("旧版事件 JSON 缺 ChannelRefundNo 时应反序列化为空字符串");
    }

    [Fact]
    public void RefundCompletedEvent_SchemaVersion_ShouldBeIncrementedToTwoWhenChannelRefundNoProvided()
    {
        // Arrange & Act — 带渠道退款流水号的构造重载应将 SchemaVersion 显式置为 2
        var evtV2 = new RefundCompletedEvent(
            orderId: Guid.NewGuid(),
            userId: Guid.NewGuid(),
            refundId: Guid.NewGuid(),
            afterSalesId: Guid.NewGuid(),
            refundAmount: 50m,
            currency: "CNY",
            completedAt: DateTime.UtcNow,
            channelRefundNo: "R20260722001");

        // Assert
        evtV2.SchemaVersion.Should().Be(2, "新增字段后 SchemaVersion 应递增到 2");
        evtV2.ChannelRefundNo.Should().Be("R20260722001");
    }
}
