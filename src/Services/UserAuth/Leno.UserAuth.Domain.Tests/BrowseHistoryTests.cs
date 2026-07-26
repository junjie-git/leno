using Leno.UserAuth.Domain.Aggregates;
using Leno.UserAuth.Domain.Exceptions;

namespace Leno.UserAuth.Domain.Tests;

public class BrowseHistoryTests
{
    #region Create

    [Fact]
    public void Create_ValidParameters_ShouldCreateHistory()
    {
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var spuId = Guid.NewGuid();
        var skuId = Guid.NewGuid();
        var viewedAt = new DateTime(2026, 7, 26, 10, 0, 0, DateTimeKind.Utc);

        var history = BrowseHistory.Create(id, userId, spuId, skuId, viewedAt);

        history.Id.Should().Be(id);
        history.UserId.Should().Be(userId);
        history.SpuId.Should().Be(spuId);
        history.SkuId.Should().Be(skuId);
        history.ViewedAt.Should().Be(viewedAt);
    }

    [Fact]
    public void Create_NullSkuId_ShouldCreateHistoryWithoutSku()
    {
        var history = BrowseHistory.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        history.SkuId.Should().BeNull();
    }

    [Fact]
    public void Create_DefaultViewedAt_ShouldUseUtcNow()
    {
        var before = DateTime.UtcNow;

        var history = BrowseHistory.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        var after = DateTime.UtcNow;
        history.ViewedAt.Should().BeOnOrAfter(before);
        history.ViewedAt.Should().BeOnOrBefore(after);
    }

    [Fact]
    public void Create_EmptyId_ShouldThrowException()
    {
        var act = () => BrowseHistory.Create(Guid.Empty, Guid.NewGuid(), Guid.NewGuid());

        act.Should().Throw<UserAuthDomainException>()
            .WithMessage("*浏览历史标识不可为空*");
    }

    [Fact]
    public void Create_EmptyUserId_ShouldThrowException()
    {
        var act = () => BrowseHistory.Create(Guid.NewGuid(), Guid.Empty, Guid.NewGuid());

        act.Should().Throw<UserAuthDomainException>()
            .WithMessage("*用户标识不可为空*");
    }

    [Fact]
    public void Create_EmptySpuId_ShouldThrowException()
    {
        var act = () => BrowseHistory.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty);

        act.Should().Throw<UserAuthDomainException>()
            .WithMessage("*SPU 标识不可为空*");
    }

    [Fact]
    public void Create_EmptySkuId_ShouldThrowException()
    {
        var act = () => BrowseHistory.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.Empty);

        act.Should().Throw<UserAuthDomainException>()
            .WithMessage("*SKU 标识不可为空*");
    }

    #endregion

    #region MarkRevisited

    [Fact]
    public void MarkRevisited_ShouldUpdateViewedAt()
    {
        var history = BrowseHistory.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var originalViewedAt = history.ViewedAt;

        var newViewedAt = originalViewedAt.AddSeconds(10);
        history.MarkRevisited(newViewedAt);

        history.ViewedAt.Should().Be(newViewedAt);
    }

    [Fact]
    public void MarkRevisited_DefaultParam_ShouldUseUtcNow()
    {
        var history = BrowseHistory.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var before = DateTime.UtcNow;

        history.MarkRevisited();

        var after = DateTime.UtcNow;
        history.ViewedAt.Should().BeOnOrAfter(before);
        history.ViewedAt.Should().BeOnOrBefore(after);
    }

    #endregion
}
