// 文件：src/BuildingBlocks/Leno.SharedContracts.Tests/Events/ReviewSubmittedEventTests.cs
using Leno.SharedContracts.Events;
using Xunit;
using FluentAssertions;
using System.Text.Json;

namespace Leno.SharedContracts.Tests.Events;

public class ReviewSubmittedEventTests
{
    [Fact]
    public void ReviewSubmittedEvent_ShouldHaveShopIdField()
    {
        // Arrange
        var evt = new ReviewSubmittedEvent(
            reviewId: Guid.NewGuid(),
            userId: Guid.NewGuid(),
            spuId: Guid.NewGuid(),
            rating: 5);

        // Act
        var shopId = evt.ShopId;

        // Assert — 字段存在且默认为 Guid.Empty（向后兼容）
        shopId.Should().Be(Guid.Empty, "ShopId 默认为 Guid.Empty 以保持向后兼容");
    }

    [Fact]
    public void ReviewSubmittedEvent_WithShopId_ShouldRoundTripThroughJson()
    {
        // Arrange
        var shopId = Guid.NewGuid();
        var evt = new ReviewSubmittedEvent(
            reviewId: Guid.NewGuid(),
            userId: Guid.NewGuid(),
            spuId: Guid.NewGuid(),
            rating: 5)
        {
            ShopId = shopId
        };

        // Act
        var json = JsonSerializer.Serialize(evt);
        var deserialized = JsonSerializer.Deserialize<ReviewSubmittedEvent>(json)!;

        // Assert
        deserialized.ShopId.Should().Be(shopId, "ShopId 应通过 JSON 序列化保留");
    }

    [Fact]
    public void ReviewApprovedEvent_ShouldAlsoHaveShopIdField()
    {
        var evt = new ReviewApprovedEvent(
            reviewId: Guid.NewGuid(),
            userId: Guid.NewGuid(),
            spuId: Guid.NewGuid(),
            rating: 4);
        evt.ShopId.Should().Be(Guid.Empty, "ReviewApprovedEvent 也应有 ShopId 字段，默认 Guid.Empty");
    }

    [Fact]
    public void ReviewHiddenEvent_ShouldAlsoHaveShopIdField()
    {
        var evt = new ReviewHiddenEvent(
            reviewId: Guid.NewGuid(),
            spuId: Guid.NewGuid(),
            rating: 1);
        evt.ShopId.Should().Be(Guid.Empty, "ReviewHiddenEvent 也应有 ShopId 字段，默认 Guid.Empty");
    }
}
