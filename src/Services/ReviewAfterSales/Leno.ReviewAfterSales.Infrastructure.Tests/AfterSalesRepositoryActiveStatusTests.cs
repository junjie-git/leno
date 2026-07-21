using Leno.ReviewAfterSales.Domain.Aggregates;
using Leno.ReviewAfterSales.Domain.ValueObjects;
using Leno.ReviewAfterSales.Infrastructure;
using Leno.ReviewAfterSales.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Leno.ReviewAfterSales.Infrastructure.Tests;

/// <summary>
/// 验证 EfCoreAfterSalesRepository.HasActiveByOrderLineAsync 活跃状态过滤：
/// - ReturnGoods=7 / ConfirmReturn=8 必须被识别为活跃状态，防止同订单行在退货流程中重复提交售后单
/// - 终态（Completed/Failed/Cancelled/Rejected）不应被识别为活跃
/// 同步验证 AsNoTracking 不影响查询结果（合并审计 3.8 部分）
/// </summary>
public sealed class AfterSalesRepositoryActiveStatusTests : IDisposable
{
    private readonly ReviewAfterSalesDbContext _context;
    private readonly EfCoreAfterSalesRepository _repo;

    public AfterSalesRepositoryActiveStatusTests()
    {
        var options = new DbContextOptionsBuilder<ReviewAfterSalesDbContext>()
            .UseInMemoryDatabase(databaseName: "aftersales_active_status_test_" + Guid.NewGuid())
            .Options;
        _context = new ReviewAfterSalesDbContext(options);
        _context.Database.EnsureCreated();
        _repo = new EfCoreAfterSalesRepository(_context);
    }

    [Theory]
    [InlineData(AfterSalesStatus.Pending)]
    [InlineData(AfterSalesStatus.Approved)]
    [InlineData(AfterSalesStatus.ReturnGoods)]
    [InlineData(AfterSalesStatus.ConfirmReturn)]
    [InlineData(AfterSalesStatus.Refunding)]
    public async Task HasActiveByOrderLineAsync_Should_Return_True_For_Active_Status(AfterSalesStatus status)
    {
        var orderLineId = Guid.NewGuid();
        var afterSales = AfterSales.Create(
            Guid.NewGuid(), Guid.NewGuid(), orderLineId, Guid.NewGuid(), Guid.NewGuid(),
            AfterSalesType.ReturnRefund, "quality", "broken", null, 10m, "CNY");
        // 反射强制推进到指定状态（避免完整状态机推进的副作用与领域事件累积）
        typeof(AfterSales).GetProperty("Status")!.SetValue(afterSales, status);

        await _repo.AddAsync(afterSales, default);
        await _context.SaveChangesAsync();

        var hasActive = await _repo.HasActiveByOrderLineAsync(orderLineId, AfterSalesType.ReturnRefund, default);

        hasActive.Should().BeTrue($"状态 {status} 应被视为活跃状态");
    }

    [Theory]
    [InlineData(AfterSalesStatus.Completed)]
    [InlineData(AfterSalesStatus.Failed)]
    [InlineData(AfterSalesStatus.Cancelled)]
    [InlineData(AfterSalesStatus.Rejected)]
    public async Task HasActiveByOrderLineAsync_Should_Return_False_For_Terminal_Status(AfterSalesStatus status)
    {
        var orderLineId = Guid.NewGuid();
        var afterSales = AfterSales.Create(
            Guid.NewGuid(), Guid.NewGuid(), orderLineId, Guid.NewGuid(), Guid.NewGuid(),
            AfterSalesType.ReturnRefund, "quality", "broken", null, 10m, "CNY");
        typeof(AfterSales).GetProperty("Status")!.SetValue(afterSales, status);

        await _repo.AddAsync(afterSales, default);
        await _context.SaveChangesAsync();

        var hasActive = await _repo.HasActiveByOrderLineAsync(orderLineId, AfterSalesType.ReturnRefund, default);

        hasActive.Should().BeFalse($"状态 {status} 为终态，不应被视为活跃状态");
    }

    [Fact]
    public async Task HasActiveByOrderLineAsync_Should_Return_False_When_Type_Mismatch()
    {
        var orderLineId = Guid.NewGuid();
        var afterSales = AfterSales.Create(
            Guid.NewGuid(), Guid.NewGuid(), orderLineId, Guid.NewGuid(), Guid.NewGuid(),
            AfterSalesType.RefundOnly, "quality", "broken", null, 10m, "CNY");

        await _repo.AddAsync(afterSales, default);
        await _context.SaveChangesAsync();

        var hasActive = await _repo.HasActiveByOrderLineAsync(orderLineId, AfterSalesType.ReturnRefund, default);

        hasActive.Should().BeFalse("售后类型不符时应返回 false");
    }

    [Fact]
    public async Task HasActiveByOrderLineAsync_Should_Return_False_When_No_Records()
    {
        var hasActive = await _repo.HasActiveByOrderLineAsync(Guid.NewGuid(), AfterSalesType.ReturnRefund, default);

        hasActive.Should().BeFalse("无记录时应返回 false");
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
