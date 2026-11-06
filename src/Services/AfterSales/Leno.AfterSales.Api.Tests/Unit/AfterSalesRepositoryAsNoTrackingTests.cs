using Leno.AfterSales.Domain.Aggregates;
using Leno.AfterSales.Domain.ValueObjects;
using Leno.AfterSales.Infrastructure;
using Leno.AfterSales.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

using FluentAssertions;

namespace Leno.AfterSales.Api.Tests.Unit;

/// <summary>
/// 审计 3.8：仓储层只读查询未使用 AsNoTracking，会进入 Change Tracker。
/// 验证售后仓储所有只读查询路径（GetByIdAsync / GetByOrderIdAsync / QueryAsync / CountAsync /
/// HasActiveByOrderLineAsync / HasActiveByOrderAsync）执行后 ChangeTracker.Entries().Count() == 0。
/// 写路径（AddAsync / UpdateAsync / RemoveAsync）不在本测试覆盖范围（保留 tracked）。
/// </summary>
public sealed class AfterSalesRepositoryAsNoTrackingTests : IDisposable
{
    private readonly AfterSalesDbContext _context;
    private readonly EfCoreAfterSalesRepository _repository;

    private readonly Guid _afterSalesId = Guid.NewGuid();
    private readonly Guid _orderId = Guid.NewGuid();
    private readonly Guid _orderLineId = Guid.NewGuid();

    public AfterSalesRepositoryAsNoTrackingTests()
    {
        var options = new DbContextOptionsBuilder<AfterSalesDbContext>()
            .UseInMemoryDatabase(databaseName: "aftersales_asnotracking_" + Guid.NewGuid())
            .Options;
        _context = new AfterSalesDbContext(options);
        _context.Database.EnsureCreated();
        _repository = new EfCoreAfterSalesRepository(_context);

        var afterSales = AfterSalesOrder.Create(
            _afterSalesId, _orderId, _orderLineId, Guid.NewGuid(), Guid.NewGuid(),
            AfterSalesType.ReturnRefund, "quality", "broken", new List<string>(), 10m, "CNY");
        _context.AfterSalesOrders.Add(afterSales);
        _context.SaveChanges();
        // 清空 ChangeTracker，确保后续测试从干净状态开始
        _context.ChangeTracker.Clear();
    }

    [Fact]
    public async Task AfterSales_GetByIdAsync_Should_Not_Track_Entity()
    {
        await _repository.GetByIdAsync(_afterSalesId, default);

        _context.ChangeTracker.Entries().Count().Should().Be(0);
    }

    [Fact]
    public async Task AfterSales_GetByOrderIdAsync_Should_Not_Track_Entities()
    {
        await _repository.GetByOrderIdAsync(_orderId, default);

        _context.ChangeTracker.Entries().Count().Should().Be(0);
    }

    [Fact]
    public async Task AfterSales_QueryAsync_Should_Not_Track_Entities()
    {
        await _repository.QueryAsync(
            orderId: _orderId, userId: null, sellerId: null, status: null,
            page: 1, pageSize: 20, ct: default);

        _context.ChangeTracker.Entries().Count().Should().Be(0);
    }

    [Fact]
    public async Task AfterSales_CountAsync_Should_Not_Track_Entities()
    {
        await _repository.CountAsync(
            orderId: _orderId, userId: null, sellerId: null, status: null, ct: default);

        _context.ChangeTracker.Entries().Count().Should().Be(0);
    }

    [Fact]
    public async Task AfterSales_HasActiveByOrderLineAsync_Should_Not_Track_Entities()
    {
        await _repository.HasActiveByOrderLineAsync(_orderLineId, AfterSalesType.ReturnRefund, default);

        _context.ChangeTracker.Entries().Count().Should().Be(0);
    }

    [Fact]
    public async Task AfterSales_HasActiveByOrderAsync_Should_Not_Track_Entities()
    {
        await _repository.HasActiveByOrderAsync(_orderId, AfterSalesType.ReturnRefund, default);

        _context.ChangeTracker.Entries().Count().Should().Be(0);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
