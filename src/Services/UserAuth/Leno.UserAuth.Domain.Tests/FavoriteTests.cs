using Leno.UserAuth.Domain.Aggregates;
using Leno.UserAuth.Domain.Exceptions;

namespace Leno.UserAuth.Domain.Tests;

public class FavoriteTests
{
    #region Create

    [Fact]
    public void Create_ValidParameters_ShouldCreateFavorite()
    {
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var spuId = Guid.NewGuid();
        var favoritedAt = new DateTime(2026, 7, 26, 10, 0, 0, DateTimeKind.Utc);

        var favorite = Favorite.Create(id, userId, spuId, favoritedAt);

        favorite.Id.Should().Be(id);
        favorite.UserId.Should().Be(userId);
        favorite.SpuId.Should().Be(spuId);
        favorite.FavoritedAt.Should().Be(favoritedAt);
    }

    [Fact]
    public void Create_DefaultFavoritedAt_ShouldUseUtcNow()
    {
        var before = DateTime.UtcNow;

        var favorite = Favorite.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        var after = DateTime.UtcNow;
        favorite.FavoritedAt.Should().BeOnOrAfter(before);
        favorite.FavoritedAt.Should().BeOnOrBefore(after);
    }

    [Fact]
    public void Create_EmptyId_ShouldThrowException()
    {
        var act = () => Favorite.Create(Guid.Empty, Guid.NewGuid(), Guid.NewGuid());

        act.Should().Throw<UserAuthDomainException>()
            .WithMessage("*收藏标识不可为空*");
    }

    [Fact]
    public void Create_EmptyUserId_ShouldThrowException()
    {
        var act = () => Favorite.Create(Guid.NewGuid(), Guid.Empty, Guid.NewGuid());

        act.Should().Throw<UserAuthDomainException>()
            .WithMessage("*用户标识不可为空*");
    }

    [Fact]
    public void Create_EmptySpuId_ShouldThrowException()
    {
        var act = () => Favorite.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty);

        act.Should().Throw<UserAuthDomainException>()
            .WithMessage("*SPU 标识不可为空*");
    }

    #endregion
}
