using Leno.UserAuth.Application.DTOs;
using Leno.UserAuth.Application.Exceptions;
using Leno.UserAuth.Application.Services;
using Leno.UserAuth.Domain.Aggregates;
using Leno.UserAuth.Domain.Exceptions;
using Leno.UserAuth.Domain.Repositories;
using Leno.SharedKernel.Abstractions;
using Moq;

namespace Leno.UserAuth.Application.Tests;

public class FavoritesAppServiceTests
{
    private readonly Mock<IFavoriteRepository> _repoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly FavoritesAppService _sut;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _otherUserId = Guid.NewGuid();

    public FavoritesAppServiceTests()
    {
        _sut = new FavoritesAppService(_repoMock.Object, _uowMock.Object);
    }

    private static Favorite CreateFavorite(Guid userId, Guid? spuId = null)
        => Favorite.Create(Guid.NewGuid(), userId, spuId ?? Guid.NewGuid());

    #region ListAsync

    [Fact]
    public async Task ListAsync_ShouldReturnPagedResult()
    {
        var favorites = new List<Favorite>
        {
            CreateFavorite(_userId),
            CreateFavorite(_userId)
        };
        _repoMock.Setup(r => r.QueryAsync(_userId, 1, 20, "created", "desc", It.IsAny<CancellationToken>()))
            .ReturnsAsync((favorites, 2));

        var query = new FavoriteQueryDto { Page = 1, PageSize = 20 };

        var result = await _sut.ListAsync(_userId, query);

        result.Items.Should().HaveCount(2);
        result.Total.Should().Be(2);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task ListAsync_Empty_ShouldReturnEmptyResult()
    {
        _repoMock.Setup(r => r.QueryAsync(_userId, 1, 20, "created", "desc", It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Favorite>(), 0));

        var query = new FavoriteQueryDto();

        var result = await _sut.ListAsync(_userId, query);

        result.Items.Should().BeEmpty();
        result.Total.Should().Be(0);
    }

    [Fact]
    public async Task ListAsync_DefaultSort_ShouldBeCreatedDesc()
    {
        _repoMock.Setup(r => r.QueryAsync(_userId, 1, 20, "created", "desc", It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Favorite>(), 0));

        var query = new FavoriteQueryDto();

        await _sut.ListAsync(_userId, query);

        _repoMock.Verify(r => r.QueryAsync(_userId, 1, 20, "created", "desc", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ListAsync_PriceAscSort_ShouldPassThrough()
    {
        _repoMock.Setup(r => r.QueryAsync(_userId, 1, 20, "price", "asc", It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Favorite>(), 0));

        var query = new FavoriteQueryDto { Sort = "price", Order = "asc" };

        await _sut.ListAsync(_userId, query);

        _repoMock.Verify(r => r.QueryAsync(_userId, 1, 20, "price", "asc", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ListAsync_InvalidSort_ShouldFallbackToCreatedDesc()
    {
        _repoMock.Setup(r => r.QueryAsync(_userId, 1, 20, "created", "desc", It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Favorite>(), 0));

        var query = new FavoriteQueryDto { Sort = "unknown", Order = "invalid" };

        await _sut.ListAsync(_userId, query);

        _repoMock.Verify(r => r.QueryAsync(_userId, 1, 20, "created", "desc", It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region AddAsync

    [Fact]
    public async Task AddAsync_NewSpu_ShouldAddFavorite()
    {
        var spuId = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByUserAndSpuAsync(_userId, spuId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Favorite?)null);
        _repoMock.Setup(r => r.CountByUserAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var dto = new AddFavoriteDto { SpuId = spuId };

        var result = await _sut.AddAsync(_userId, dto);

        result.SpuId.Should().Be(spuId);
        _repoMock.Verify(r => r.AddAsync(It.IsAny<Favorite>(), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddAsync_AlreadyFavorited_ShouldReturnExisting()
    {
        var spuId = Guid.NewGuid();
        var existing = Favorite.Create(Guid.NewGuid(), _userId, spuId);
        _repoMock.Setup(r => r.GetByUserAndSpuAsync(_userId, spuId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var dto = new AddFavoriteDto { SpuId = spuId };

        var result = await _sut.AddAsync(_userId, dto);

        result.FavoriteId.Should().Be(existing.Id);
        _repoMock.Verify(r => r.AddAsync(It.IsAny<Favorite>(), It.IsAny<CancellationToken>()), Times.Never);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddAsync_EmptySpuId_ShouldThrowValidationException()
    {
        var dto = new AddFavoriteDto { SpuId = Guid.Empty };

        var act = () => _sut.AddAsync(_userId, dto);

        await act.Should().ThrowAsync<UserAuthValidationException>()
            .WithMessage("*SPU 标识不可为空*");
    }

    [Fact]
    public async Task AddAsync_LimitExceeded_ShouldThrowDomainException()
    {
        var spuId = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByUserAndSpuAsync(_userId, spuId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Favorite?)null);
        _repoMock.Setup(r => r.CountByUserAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(FavoritesAppService.MaxFavoritesPerUser);

        var dto = new AddFavoriteDto { SpuId = spuId };

        var act = () => _sut.AddAsync(_userId, dto);

        await act.Should().ThrowAsync<UserAuthDomainException>()
            .WithMessage("*最多收藏*");
    }

    #endregion

    #region RemoveAsync

    [Fact]
    public async Task RemoveAsync_Existing_ShouldRemove()
    {
        var spuId = Guid.NewGuid();
        var existing = Favorite.Create(Guid.NewGuid(), _userId, spuId);
        _repoMock.Setup(r => r.GetByUserAndSpuAsync(_userId, spuId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        await _sut.RemoveAsync(_userId, spuId);

        _repoMock.Verify(r => r.RemoveAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemoveAsync_NotFavorited_ShouldBeIdempotent()
    {
        var spuId = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByUserAndSpuAsync(_userId, spuId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Favorite?)null);

        await _sut.RemoveAsync(_userId, spuId);

        _repoMock.Verify(r => r.RemoveAsync(It.IsAny<Favorite>(), It.IsAny<CancellationToken>()), Times.Never);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RemoveAsync_EmptySpuId_ShouldThrowValidationException()
    {
        var act = () => _sut.RemoveAsync(_userId, Guid.Empty);

        await act.Should().ThrowAsync<UserAuthValidationException>();
    }

    [Fact]
    public async Task RemoveAsync_OtherUserFavorite_ShouldNotRemove()
    {
        // 用户隔离：用户 A 调用 Remove 时只能查到自己 user_id 下的收藏，
        // GetByUserAndSpuAsync 已强制按 userId 过滤，返回 null 时直接幂等返回成功
        var spuId = Guid.NewGuid();
        var otherUserFavorite = Favorite.Create(Guid.NewGuid(), _otherUserId, spuId);
        _repoMock.Setup(r => r.GetByUserAndSpuAsync(_userId, spuId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Favorite?)null);
        _repoMock.Setup(r => r.GetByUserAndSpuAsync(_otherUserId, spuId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(otherUserFavorite);

        await _sut.RemoveAsync(_userId, spuId);

        _repoMock.Verify(r => r.RemoveAsync(otherUserFavorite, It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region BatchDeleteAsync

    [Fact]
    public async Task BatchDeleteAsync_Valid_ShouldReturnDeletedCount()
    {
        var spuIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        _repoMock.Setup(r => r.BatchDeleteAsync(_userId, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var dto = new BatchDeleteFavoritesDto { SpuIds = spuIds };

        var result = await _sut.BatchDeleteAsync(_userId, dto);

        result.Should().Be(2);
    }

    [Fact]
    public async Task BatchDeleteAsync_EmptyList_ShouldThrowValidationException()
    {
        var dto = new BatchDeleteFavoritesDto { SpuIds = new List<Guid>() };

        var act = () => _sut.BatchDeleteAsync(_userId, dto);

        await act.Should().ThrowAsync<UserAuthValidationException>()
            .WithMessage("*SPU 列表不可为空*");
    }

    [Fact]
    public async Task BatchDeleteAsync_ExceedBatchSize_ShouldThrowValidationException()
    {
        var spuIds = Enumerable.Range(0, FavoritesAppService.MaxBatchSize + 1).Select(_ => Guid.NewGuid()).ToList();
        var dto = new BatchDeleteFavoritesDto { SpuIds = spuIds };

        var act = () => _sut.BatchDeleteAsync(_userId, dto);

        await act.Should().ThrowAsync<UserAuthValidationException>()
            .WithMessage("*批量操作上限*");
    }

    [Fact]
    public async Task BatchDeleteAsync_ContainsEmptyGuid_ShouldThrowValidationException()
    {
        var spuIds = new List<Guid> { Guid.NewGuid(), Guid.Empty };
        var dto = new BatchDeleteFavoritesDto { SpuIds = spuIds };

        var act = () => _sut.BatchDeleteAsync(_userId, dto);

        await act.Should().ThrowAsync<UserAuthValidationException>()
            .WithMessage("*SPU 标识不可为空 GUID*");
    }

    [Fact]
    public async Task BatchDeleteAsync_Duplicates_ShouldBeDeduplicated()
    {
        var spuId = Guid.NewGuid();
        var spuIds = new List<Guid> { spuId, spuId, spuId };
        _repoMock.Setup(r => r.BatchDeleteAsync(_userId, It.Is<IReadOnlyCollection<Guid>>(c => c.Count == 1), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var dto = new BatchDeleteFavoritesDto { SpuIds = spuIds };

        var result = await _sut.BatchDeleteAsync(_userId, dto);

        result.Should().Be(1);
    }

    #endregion

    #region CountAsync

    [Fact]
    public async Task CountAsync_ShouldReturnCount()
    {
        _repoMock.Setup(r => r.CountByUserAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);

        var result = await _sut.CountAsync(_userId);

        result.Count.Should().Be(42);
    }

    [Fact]
    public async Task CountAsync_Zero_ShouldReturnZero()
    {
        _repoMock.Setup(r => r.CountByUserAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var result = await _sut.CountAsync(_userId);

        result.Count.Should().Be(0);
    }

    #endregion
}
