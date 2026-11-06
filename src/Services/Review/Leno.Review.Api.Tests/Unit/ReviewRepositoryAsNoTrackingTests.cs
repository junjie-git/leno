using Leno.Review.Domain.ValueObjects;
using Leno.Review.Infrastructure;
using Leno.Review.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

using FluentAssertions;

using ReviewAggregate = Leno.Review.Domain.Aggregates.Review;

namespace Leno.Review.Api.Tests.Unit;

/// <summary>
/// 审计 3.8：仓储层只读查询未使用 AsNoTracking，会进入 Change Tracker。
/// 验证评价仓储所有只读查询路径（GetByIdAsync / GetByOrderLineAsync / ExistsByOrderLineAsync /
/// QueryAsync / CountAsync / GetBySpuIdAsync / GetByOrderIdAsync / GetRatingSnapshotAsync）
/// 执行后 ChangeTracker.Entries().Count() == 0。
/// 写路径（AddAsync / UpdateAsync / RemoveAsync）不在本测试覆盖范围（保留 tracked）。
/// </summary>
public sealed class ReviewRepositoryAsNoTrackingTests : IDisposable
{
    private readonly ReviewDbContext _context;
    private readonly EfCoreReviewRepository _repository;

    private readonly Guid _orderId = Guid.NewGuid();
    private readonly Guid _orderLineId = Guid.NewGuid();
    private readonly Guid _spuId = Guid.NewGuid();
    private readonly Guid _reviewId = Guid.NewGuid();

    public ReviewRepositoryAsNoTrackingTests()
    {
        var options = new DbContextOptionsBuilder<ReviewDbContext>()
            .UseInMemoryDatabase(databaseName: "review_asnotracking_" + Guid.NewGuid())
            .Options;
        _context = new ReviewDbContext(options);
        _context.Database.EnsureCreated();
        _repository = new EfCoreReviewRepository(_context);

        var review = ReviewAggregate.Create(
            _reviewId, _orderId, _orderLineId, _spuId, Guid.NewGuid(),
            Guid.NewGuid(), rating: 5, "good", new List<string>(), sellerId: Guid.NewGuid());
        // 推进到 Approved 状态，使 GetRatingSnapshotAsync 的 Approved 过滤条件命中
        review.Approve(Guid.NewGuid());
        _context.Reviews.Add(review);
        _context.SaveChanges();
        // 清空 ChangeTracker，确保后续测试从干净状态开始
        _context.ChangeTracker.Clear();
    }

    [Fact]
    public async Task Review_GetByIdAsync_Should_Not_Track_Entity()
    {
        await _repository.GetByIdAsync(_reviewId, default);

        _context.ChangeTracker.Entries().Count().Should().Be(0);
    }

    [Fact]
    public async Task Review_GetByOrderLineAsync_Should_Not_Track_Entity()
    {
        await _repository.GetByOrderLineAsync(_orderLineId, default);

        _context.ChangeTracker.Entries().Count().Should().Be(0);
    }

    [Fact]
    public async Task Review_ExistsByOrderLineAsync_Should_Not_Track_Entities()
    {
        await _repository.ExistsByOrderLineAsync(_orderLineId, default);

        _context.ChangeTracker.Entries().Count().Should().Be(0);
    }

    [Fact]
    public async Task Review_QueryAsync_Should_Not_Track_Entities()
    {
        await _repository.QueryAsync(
            spuId: _spuId, userId: null, status: null,
            page: 1, pageSize: 20, ct: default);

        _context.ChangeTracker.Entries().Count().Should().Be(0);
    }

    [Fact]
    public async Task Review_CountAsync_Should_Not_Track_Entities()
    {
        await _repository.CountAsync(
            spuId: _spuId, userId: null, status: null, ct: default);

        _context.ChangeTracker.Entries().Count().Should().Be(0);
    }

    [Fact]
    public async Task Review_GetBySpuIdAsync_Should_Not_Track_Entities()
    {
        await _repository.GetBySpuIdAsync(_spuId, ReviewStatus.Approved, default);

        _context.ChangeTracker.Entries().Count().Should().Be(0);
    }

    [Fact]
    public async Task Review_GetByOrderIdAsync_Should_Not_Track_Entities()
    {
        await _repository.GetByOrderIdAsync(_orderId, ReviewStatus.Approved, default);

        _context.ChangeTracker.Entries().Count().Should().Be(0);
    }

    [Fact]
    public async Task Review_GetRatingSnapshotAsync_Should_Not_Track_Entities()
    {
        await _repository.GetRatingSnapshotAsync(_spuId, default);

        _context.ChangeTracker.Entries().Count().Should().Be(0);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
