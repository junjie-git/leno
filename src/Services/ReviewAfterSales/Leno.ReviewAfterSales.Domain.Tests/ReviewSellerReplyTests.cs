using Leno.ReviewAfterSales.Domain.Aggregates;
using Leno.ReviewAfterSales.Domain.Exceptions;

namespace Leno.ReviewAfterSales.Domain.Tests;

/// <summary>
/// P0-2.7 专项测试：验证 SellerReply 卖家归属校验。
/// 聚合 Create 工厂接收 sellerId 并存储为 SellerId 字段；
/// SellerReply 方法校验传入 sellerId 必须等于 SellerId，否则抛 REVIEW_NOT_OWNED。
/// </summary>
public sealed class ReviewSellerReplyTests
{
    [Fact]
    public void SellerReply_Should_Throw_When_SellerId_Mismatch()
    {
        // Arrange
        var sellerId = Guid.NewGuid();
        var review = Review.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), 5, "good", null, sellerId: sellerId);
        review.Approve(Guid.NewGuid());

        var attacker = Guid.NewGuid();

        // Act
        var ex = Assert.Throws<ReviewDomainException>(() => review.SellerReply(attacker, "reply content"));

        // Assert
        Assert.Equal("REVIEW_NOT_OWNED", ex.ErrorCode);
        Assert.Null(review.SellerReplyContent);
        Assert.Null(review.SellerReplyBy);
        Assert.Null(review.SellerReplyAt);
    }

    [Fact]
    public void SellerReply_Should_Record_SellerReplyBy_When_Match()
    {
        // Arrange
        var sellerId = Guid.NewGuid();
        var review = Review.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), 5, "good", null, sellerId: sellerId);
        review.Approve(Guid.NewGuid());

        // Act
        review.SellerReply(sellerId, "thanks");

        // Assert
        Assert.Equal("thanks", review.SellerReplyContent);
        Assert.Equal(sellerId, review.SellerReplyBy);
        Assert.True(review.SellerReplyAt.HasValue);
    }

    [Fact]
    public void SellerReply_Should_Throw_When_SellerId_Empty()
    {
        // Arrange
        var sellerId = Guid.NewGuid();
        var review = Review.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), 5, "good", null, sellerId: sellerId);
        review.Approve(Guid.NewGuid());

        // Act
        var ex = Assert.Throws<ReviewDomainException>(() => review.SellerReply(Guid.Empty, "reply content"));

        // Assert
        Assert.Equal("REVIEW_SELLER_EMPTY", ex.ErrorCode);
    }

    [Fact]
    public void Create_Should_Throw_When_SellerId_Empty()
    {
        // Act
        var ex = Assert.Throws<ReviewDomainException>(() => Review.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), 5, "good", null, sellerId: Guid.Empty));

        // Assert
        Assert.Equal("REVIEW_SELLER_EMPTY", ex.ErrorCode);
    }
}
