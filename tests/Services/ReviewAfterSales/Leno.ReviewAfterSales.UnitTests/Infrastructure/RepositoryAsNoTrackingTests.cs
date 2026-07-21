using Leno.ReviewAfterSales.Domain.Aggregates;
using Leno.ReviewAfterSales.Domain.ValueObjects;
using Leno.ReviewAfterSales.Infrastructure;
using Leno.ReviewAfterSales.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Leno.ReviewAfterSales.UnitTests.Infrastructure;

/// <summary>
/// 审计 3.8：仓储层全部未使用 AsNoTracking，只读查询进入 Change Tracker。
/// 验证所有只读查询路径（GetByIdAsync / GetByOrderIdAsync / QueryAsync / CountAsync /
/// HasActiveByOrderLineAsync / HasActiveByOrderAsync / ExistsByOrderLineAsync /
/// GetBySpuIdAsync / GetByOrderLineAsync / GetByOrderIdAsync / GetRatingSnapshotAsync）
/// 查询完成后 ChangeTracker.Entries().Count() == 0，避免无谓的跟踪开销与脏写风险。
/// 写路径（AddAsync / UpdateAsync / RemoveAsync）不在本测试覆盖范围（保留 tracked）。
/// </summary>
public sealed class RepositoryAsNoTrackingTests : IDisposable
{
    private readonly ReviewAfterSalesDbContext _context;
    private readonly EfCoreAfterSalesRepository _afterSalesRepo;
    private readonly EfCoreReviewRepository _reviewRepo;

    private readonly Guid _afterSalesId = Guid.NewGuid();
    private readonly Guid _orderId = Guid.NewGuid();
    private readonly Guid _orderLineId = Guid.NewGuid();
    private readonly Guid _spuId = Guid.NewGuid();
    private readonly Guid _reviewId = Guid.NewGuid();

    public RepositoryAsNoTrackingTests()
    {
        var options = new DbContextOptionsBuilder<ReviewAfterSalesDbContext>()
            .UseInMemoryDatabase(databaseName: "asnotracking_test_" + Guid.NewGuid())
            .Options;
        _context = new ReviewAfterSalesDbContext(options);
        _context.Database.EnsureCreated();
        _afterSalesRepo = new EfCoreAfterSalesRepository(_context);
        _reviewRepo = new EfCoreReviewRepository(_context);

        SeedData();
    }

    private void SeedData()
    {
        var afterSales = AfterSales.Create(
            _afterSalesId, _orderId, _orderLineId, Guid.NewGuid(), Guid.NewGuid(),
            AfterSalesType.ReturnRefund, "quality", "broken", null, 10m, "CNY");
        _context.AfterSales.Add(afterSales);

        var review = Review.Create(
            _reviewId, _orderId, _orderLineId, _spuId, Guid.NewGuid(),
            Guid.NewGuid(), rating: 5, "good", null, sellerId: Guid.NewGuid());
        // 推进到 Approved 状态，使 GetRatingSnapshotAsync 的 Approved 过滤条件命中
        review.Approve(Guid.NewGuid());
        _context.Reviews.Add(review);

        _context.SaveChanges();
        // 清空 ChangeTracker，确保后续测试从干净状态开始
        _context.ChangeTracker.Clear();
    }

    [Fact]
    public async Task AfterSales_GetByIdAsync_Should_Not_Track_Entity()
    {
        await _afterSalesRepo.GetByIdAsync(_afterSalesId, default);

        _context.ChangeTracker.Entries().Count().Should().Be(0);
    }

    [Fact]
    public async Task AfterSales_GetByOrderIdAsync_Should_Not_Track_Entities()
    {
        await _afterSalesRepo.GetByOrderIdAsync(_orderId, default);

        _context.ChangeTracker.Entries().Count().Should().Be(0);
    }

    [Fact]
    public async Task AfterSales_QueryAsync_Should_Not_Track_Entities()
    {
        await _afterSalesRepo.QueryAsync(
            orderId: _orderId, userId: null, sellerId: null, status: null,
            page: 1, pageSize: 20, ct: default);

        _context.ChangeTracker.Entries().Count().Should().Be(0);
    }

    [Fact]
    public async Task AfterSales_CountAsync_Should_Not_Track_Entities()
    {
        await _afterSalesRepo.CountAsync(
            orderId: _orderId, userId: null, sellerId: null, status: null, ct: default);

        _context.ChangeTracker.Entries().Count().Should().Be(0);
    }

    [Fact]
    public async Task AfterSales_HasActiveByOrderLineAsync_Should_Not_Track_Entities()
    {
        await _afterSalesRepo.HasActiveByOrderLineAsync(_orderLineId, AfterSalesType.ReturnRefund, default);

        _context.ChangeTracker.Entries().Count().Should().Be(0);
    }

    [Fact]
    public async Task AfterSales_HasActiveByOrderAsync_Should_Not_Track_Entities()
    {
        await _afterSalesRepo.HasActiveByOrderAsync(_orderId, AfterSalesType.ReturnRefund, default);

        _context.ChangeTracker.Entries().Count().Should().Be(0);
    }

    [Fact]
    public async Task Review_GetByIdAsync_Should_Not_Track_Entity()
    {
        await _reviewRepo.GetByIdAsync(_reviewId, default);

        _context.ChangeTracker.Entries().Count().Should().Be(0);
    }

    [Fact]
    public async Task Review_GetByOrderLineAsync_Should_Not_Track_Entity()
    {
        await _reviewRepo.GetByOrderLineAsync(_orderLineId, default);

        _context.ChangeTracker.Entries().Count().Should().Be(0);
    }

    [Fact]
    public async Task Review_ExistsByOrderLineAsync_Should_Not_Track_Entities()
    {
        await _reviewRepo.ExistsByOrderLineAsync(_orderLineId, default);

        _context.ChangeTracker.Entries().Count().Should().Be(0);
    }

    [Fact]
    public async Task Review_QueryAsync_Should_Not_Track_Entities()
    {
        await _reviewRepo.QueryAsync(
            spuId: _spuId, userId: null, status: null,
            page: 1, pageSize: 20, ct: default);

        _context.ChangeTracker.Entries().Count().Should().Be(0);
    }

    [Fact]
    public async Task Review_CountAsync_Should_Not_Track_Entities()
    {
        await _reviewRepo.CountAsync(
            spuId: _spuId, userId: null, status: null, ct: default);

        _context.ChangeTracker.Entries().Count().Should().Be(0);
    }

    [Fact]
    public async Task Review_GetBySpuIdAsync_Should_Not_Track_Entities()
    {
        await _reviewRepo.GetBySpuIdAsync(_spuId, ReviewStatus.Approved, default);

        _context.ChangeTracker.Entries().Count().Should().Be(0);
    }

    [Fact]
    public async Task Review_GetByOrderIdAsync_Should_Not_Track_Entities()
    {
        await _reviewRepo.GetByOrderIdAsync(_orderId, ReviewStatus.Approved, default);

        _context.ChangeTracker.Entries().Count().Should().Be(0);
    }

    [Fact]
    public async Task Review_GetRatingSnapshotAsync_Should_Not_Track_Entities()
    {
        await _reviewRepo.GetRatingSnapshotAsync(_spuId, default);

        _context.ChangeTracker.Entries().Count().Should().Be(0);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
