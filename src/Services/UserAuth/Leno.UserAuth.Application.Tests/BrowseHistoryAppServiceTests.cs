using Leno.UserAuth.Application.DTOs;
using Leno.UserAuth.Application.Exceptions;
using Leno.UserAuth.Application.Services;
using Leno.UserAuth.Domain.Aggregates;
using Leno.UserAuth.Domain.Exceptions;
using Leno.UserAuth.Domain.Repositories;
using Leno.SharedKernel.Abstractions;
using Moq;

namespace Leno.UserAuth.Application.Tests;

public class BrowseHistoryAppServiceTests
{
    private readonly Mock<IBrowseHistoryRepository> _repoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly BrowseHistoryAppService _sut;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _otherUserId = Guid.NewGuid();

    public BrowseHistoryAppServiceTests()
    {
        _sut = new BrowseHistoryAppService(_repoMock.Object, _uowMock.Object);
    }

    private static BrowseHistory CreateHistory(Guid userId, Guid? spuId = null, DateTime? viewedAt = null)
        => BrowseHistory.Create(Guid.NewGuid(), userId, spuId ?? Guid.NewGuid(), null, viewedAt);

    #region ListAsync

    [Fact]
    public async Task ListAsync_ShouldReturnPagedResult()
    {
        var items = new List<BrowseHistory>
        {
            CreateHistory(_userId),
            CreateHistory(_userId)
        };
        _repoMock.Setup(r => r.QueryAsync(_userId, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((items, 2));

        var query = new BrowseHistoryQueryDto { Page = 1, PageSize = 20 };

        var result = await _sut.ListAsync(_userId, query);

        result.Items.Should().HaveCount(2);
        result.Total.Should().Be(2);
    }

    [Fact]
    public async Task ListAsync_Empty_ShouldReturnEmptyResult()
    {
        _repoMock.Setup(r => r.QueryAsync(_userId, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<BrowseHistory>(), 0));

        var result = await _sut.ListAsync(_userId, new BrowseHistoryQueryDto());

        result.Items.Should().BeEmpty();
        result.Total.Should().Be(0);
    }

    #endregion

    #region AddAsync

    [Fact]
    public async Task AddAsync_NewSpu_ShouldAddHistory()
    {
        var spuId = Guid.NewGuid();
        _repoMock.Setup(r => r.FindLatestByUserAndSpuAsync(_userId, spuId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BrowseHistory?)null);
        _repoMock.Setup(r => r.QueryAsync(_userId, 1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<BrowseHistory>(), 0));

        var dto = new AddBrowseHistoryDto { SpuId = spuId };

        var result = await _sut.AddAsync(_userId, dto);

        result.SpuId.Should().Be(spuId);
        _repoMock.Verify(r => r.AddAsync(It.IsAny<BrowseHistory>(), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddAsync_RecentDuplicate_ShouldUpdateViewedAtOnly()
    {
        var spuId = Guid.NewGuid();
        var recentTime = DateTime.UtcNow.AddSeconds(-1); // 1 秒前，在 5 秒窗口内
        var existing = BrowseHistory.Create(Guid.NewGuid(), _userId, spuId, null, recentTime);
        _repoMock.Setup(r => r.FindLatestByUserAndSpuAsync(_userId, spuId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var dto = new AddBrowseHistoryDto { SpuId = spuId };

        var result = await _sut.AddAsync(_userId, dto);

        result.HistoryId.Should().Be(existing.Id);
        _repoMock.Verify(r => r.UpdateAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(r => r.AddAsync(It.IsAny<BrowseHistory>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddAsync_OldDuplicate_ShouldAddNewRecord()
    {
        var spuId = Guid.NewGuid();
        var oldTime = DateTime.UtcNow.AddSeconds(-10); // 10 秒前，超出 5 秒窗口
        var existing = BrowseHistory.Create(Guid.NewGuid(), _userId, spuId, null, oldTime);
        _repoMock.Setup(r => r.FindLatestByUserAndSpuAsync(_userId, spuId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _repoMock.Setup(r => r.QueryAsync(_userId, 1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<BrowseHistory>(), 0));

        var dto = new AddBrowseHistoryDto { SpuId = spuId };

        var result = await _sut.AddAsync(_userId, dto);

        result.HistoryId.Should().NotBe(existing.Id);
        _repoMock.Verify(r => r.AddAsync(It.IsAny<BrowseHistory>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddAsync_EmptySpuId_ShouldThrowValidationException()
    {
        var dto = new AddBrowseHistoryDto { SpuId = Guid.Empty };

        var act = () => _sut.AddAsync(_userId, dto);

        await act.Should().ThrowAsync<UserAuthValidationException>()
            .WithMessage("*SPU 标识不可为空*");
    }

    [Fact]
    public async Task AddAsync_EmptySkuId_ShouldThrowValidationException()
    {
        var dto = new AddBrowseHistoryDto { SpuId = Guid.NewGuid(), SkuId = Guid.Empty };

        var act = () => _sut.AddAsync(_userId, dto);

        await act.Should().ThrowAsync<UserAuthValidationException>()
            .WithMessage("*SKU 标识不可为空 GUID*");
    }

    [Fact]
    public async Task AddAsync_WithSku_ShouldPersistSkuId()
    {
        var spuId = Guid.NewGuid();
        var skuId = Guid.NewGuid();
        _repoMock.Setup(r => r.FindLatestByUserAndSpuAsync(_userId, spuId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BrowseHistory?)null);
        _repoMock.Setup(r => r.QueryAsync(_userId, 1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<BrowseHistory>(), 0));

        var dto = new AddBrowseHistoryDto { SpuId = spuId, SkuId = skuId };

        var result = await _sut.AddAsync(_userId, dto);

        result.SkuId.Should().Be(skuId);
    }

    #endregion

    #region RemoveAsync

    [Fact]
    public async Task RemoveAsync_OwnedHistory_ShouldRemove()
    {
        var history = CreateHistory(_userId);
        _repoMock.Setup(r => r.GetByIdAsync(history.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(history);

        await _sut.RemoveAsync(_userId, history.Id);

        _repoMock.Verify(r => r.RemoveAsync(history, It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemoveAsync_NotFound_ShouldThrowDomainException()
    {
        var id = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BrowseHistory?)null);

        var act = () => _sut.RemoveAsync(_userId, id);

        await act.Should().ThrowAsync<UserAuthDomainException>()
            .WithMessage("*浏览历史不存在*");
    }

    [Fact]
    public async Task RemoveAsync_OtherUserHistory_ShouldThrowDomainException()
    {
        // 用户隔离：用户 A 不能删除用户 B 的浏览历史
        var otherUserHistory = CreateHistory(_otherUserId);
        _repoMock.Setup(r => r.GetByIdAsync(otherUserHistory.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(otherUserHistory);

        var act = () => _sut.RemoveAsync(_userId, otherUserHistory.Id);

        await act.Should().ThrowAsync<UserAuthDomainException>()
            .WithMessage("*无权操作他人*");
    }

    [Fact]
    public async Task RemoveAsync_EmptyId_ShouldThrowValidationException()
    {
        var act = () => _sut.RemoveAsync(_userId, Guid.Empty);

        await act.Should().ThrowAsync<UserAuthValidationException>();
    }

    #endregion

    #region BatchDeleteAsync

    [Fact]
    public async Task BatchDeleteAsync_Valid_ShouldReturnDeletedCount()
    {
        var ids = new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        _repoMock.Setup(r => r.BatchDeleteAsync(_userId, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var dto = new BatchDeleteBrowseHistoryDto { Ids = ids };

        var result = await _sut.BatchDeleteAsync(_userId, dto);

        result.Should().Be(2);
    }

    [Fact]
    public async Task BatchDeleteAsync_EmptyList_ShouldThrowValidationException()
    {
        var dto = new BatchDeleteBrowseHistoryDto { Ids = new List<Guid>() };

        var act = () => _sut.BatchDeleteAsync(_userId, dto);

        await act.Should().ThrowAsync<UserAuthValidationException>()
            .WithMessage("*ID 列表不可为空*");
    }

    [Fact]
    public async Task BatchDeleteAsync_ExceedBatchSize_ShouldThrowValidationException()
    {
        var ids = Enumerable.Range(0, BrowseHistoryAppService.MaxBatchSize + 1).Select(_ => Guid.NewGuid()).ToList();
        var dto = new BatchDeleteBrowseHistoryDto { Ids = ids };

        var act = () => _sut.BatchDeleteAsync(_userId, dto);

        await act.Should().ThrowAsync<UserAuthValidationException>()
            .WithMessage("*批量操作上限*");
    }

    [Fact]
    public async Task BatchDeleteAsync_ContainsEmptyGuid_ShouldThrowValidationException()
    {
        var ids = new List<Guid> { Guid.NewGuid(), Guid.Empty };
        var dto = new BatchDeleteBrowseHistoryDto { Ids = ids };

        var act = () => _sut.BatchDeleteAsync(_userId, dto);

        await act.Should().ThrowAsync<UserAuthValidationException>()
            .WithMessage("*标识不可为空 GUID*");
    }

    [Fact]
    public async Task BatchDeleteAsync_Duplicates_ShouldBeDeduplicated()
    {
        var id = Guid.NewGuid();
        var ids = new List<Guid> { id, id, id };
        _repoMock.Setup(r => r.BatchDeleteAsync(_userId, It.Is<IReadOnlyCollection<Guid>>(c => c.Count == 1), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var dto = new BatchDeleteBrowseHistoryDto { Ids = ids };

        var result = await _sut.BatchDeleteAsync(_userId, dto);

        result.Should().Be(1);
    }

    #endregion

    #region ClearAllAsync

    [Fact]
    public async Task ClearAllAsync_ShouldReturnDeletedCount()
    {
        _repoMock.Setup(r => r.ClearAllByUserAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(15);

        var result = await _sut.ClearAllAsync(_userId);

        result.Should().Be(15);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ClearAllAsync_Empty_ShouldReturnZero()
    {
        _repoMock.Setup(r => r.ClearAllByUserAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var result = await _sut.ClearAllAsync(_userId);

        result.Should().Be(0);
    }

    [Fact]
    public async Task ClearAllAsync_OtherUserData_ShouldNotBeAffected()
    {
        // 用户隔离：ClearAllByUserAsync 强制按 userId 过滤
        _repoMock.Setup(r => r.ClearAllByUserAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        await _sut.ClearAllAsync(_userId);

        _repoMock.Verify(r => r.ClearAllByUserAsync(_userId, It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(r => r.ClearAllByUserAsync(_otherUserId, It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion
}
