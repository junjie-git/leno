using Leno.UserCenter.Domain.Aggregates;

namespace Leno.UserCenter.Domain.Tests.Aggregates;

/// <summary>
/// Favorite（商品收藏聚合）与 BrowseHistory（浏览历史聚合）单元测试：
/// 工厂校验 + 时间缺省/显式指定 + 重浏览时间刷新。
/// </summary>
public class FavoriteAndBrowseHistoryTests
{
    #region Favorite

    [Fact]
    public void Favorite_Create_With_Valid_Input_Should_Initialize()
    {
        var userId = Guid.NewGuid();
        var spuId = Guid.NewGuid();
        var at = new DateTime(2026, 9, 24, 8, 0, 0, DateTimeKind.Utc);

        var favorite = Favorite.Create(Guid.NewGuid(), userId, spuId, at);

        favorite.UserId.Should().Be(userId);
        favorite.SpuId.Should().Be(spuId);
        favorite.FavoritedAt.Should().Be(at);
    }

    [Fact]
    public void Favorite_Create_Without_Time_Should_Default_To_UtcNow()
    {
        var before = DateTime.UtcNow;

        var favorite = Favorite.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        favorite.FavoritedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTime.UtcNow);
    }

    [Theory]
    [InlineData("id")]
    [InlineData("userId")]
    [InlineData("spuId")]
    public void Favorite_Create_With_Empty_Id_Should_Throw(string emptyField)
    {
        var id = emptyField == "id" ? Guid.Empty : Guid.NewGuid();
        var userId = emptyField == "userId" ? Guid.Empty : Guid.NewGuid();
        var spuId = emptyField == "spuId" ? Guid.Empty : Guid.NewGuid();

        var create = () => Favorite.Create(id, userId, spuId);

        create.Should().Throw<Leno.UserCenter.Domain.Exceptions.UserCenterDomainException>();
    }

    #endregion

    #region BrowseHistory

    [Fact]
    public void BrowseHistory_Create_Without_Sku_Should_Allow_Null_SkuId()
    {
        var history = BrowseHistory.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        history.SkuId.Should().BeNull("进入详情页时可能未选 SKU");
        history.ViewedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void BrowseHistory_Create_With_Empty_Guid_Sku_Should_Throw()
    {
        var create = () => BrowseHistory.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), skuId: Guid.Empty);

        create.Should().Throw<Leno.UserCenter.Domain.Exceptions.UserCenterDomainException>()
            .WithMessage("*SKU 标识不可为空 GUID*");
    }

    [Fact]
    public void BrowseHistory_MarkRevisited_Should_Refresh_ViewedAt_Without_New_Record()
    {
        var original = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        var history = BrowseHistory.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), viewedAt: original);
        var revisitAt = new DateTime(2026, 9, 24, 8, 30, 0, DateTimeKind.Utc);

        history.MarkRevisited(revisitAt);

        history.ViewedAt.Should().Be(revisitAt, "重复浏览应刷新时间而非新增记录");
    }

    #endregion
}
