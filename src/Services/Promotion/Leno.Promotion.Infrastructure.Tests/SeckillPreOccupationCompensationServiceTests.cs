using Leno.Promotion.Domain.Aggregates;
using Leno.Promotion.Domain.Repositories;
using Leno.Promotion.Domain.Services;
using Leno.Promotion.Infrastructure.BackgroundServices;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace Leno.Promotion.Infrastructure.Tests;

/// <summary>
/// P0-2.4 测试：覆盖 SeckillPreOccupationCompensationService 在 TOCTOU 竞态场景下的状态守卫行为。
/// </summary>
public class SeckillPreOccupationCompensationServiceTests
{
    private readonly Mock<ISeckillPreOccupationRecordRepository> _recordRepoMock = new();
    private readonly Mock<ISeckillActivityRepository> _activityRepoMock = new();
    private readonly Mock<ISeckillStockService> _stockServiceMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IUnitOfWorkTransaction> _txMock = new();

    private static readonly Guid ActivityId = Guid.NewGuid();
    private static readonly Guid SkuId = Guid.NewGuid();

    [Fact]
    public async Task Compensate_RecordFulfilledDuringWindow_ShouldSkipAndNotRestore()
    {
        // 模拟竞态：补偿读取后，履约事件先行落库（IsFulfilled=true），
        // 补偿不应继续回退库存，否则产生 IsFulfilled=true && IsRolledBack=true 非法状态
        var record = CreateRecord();
        record.MarkFulfilled();
        _recordRepoMock.Setup(r => r.GetUnfulfilledAsync(It.IsAny<DateTime>(), 0, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SeckillPreOccupationRecord> { record });
        _recordRepoMock.Setup(r => r.GetByIdAsync(record.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);
        _unitOfWorkMock.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_txMock.Object);
        _txMock.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await InvokeCompensateAsync();

        _stockServiceMock.Verify(
            s => s.RestoreAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _txMock.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Compensate_ValidRecord_ShouldRestoreAndMarkRolledBack()
    {
        var activity = SeckillActivity.Create(ActivityId, Guid.NewGuid(), SkuId, 99m, 199m, 100, 1,
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(2));
        activity.Activate();
        var record = CreateRecord();
        _recordRepoMock.Setup(r => r.GetUnfulfilledAsync(It.IsAny<DateTime>(), 0, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SeckillPreOccupationRecord> { record });
        _recordRepoMock.Setup(r => r.GetByIdAsync(record.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);
        _activityRepoMock.Setup(r => r.GetByIdAsync(ActivityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activity);
        _unitOfWorkMock.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_txMock.Object);
        _unitOfWorkMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _stockServiceMock.Setup(s => s.RestoreAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _txMock.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await InvokeCompensateAsync();

        record.IsRolledBack.Should().BeTrue();
        _stockServiceMock.Verify(
            s => s.RestoreAsync(ActivityId, SkuId, 1, It.IsAny<CancellationToken>()), Times.Once);
        _txMock.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private async Task InvokeCompensateAsync()
    {
        var scopeFactory = new ServiceCollection()
            .AddSingleton(_recordRepoMock.Object)
            .AddSingleton(_activityRepoMock.Object)
            .AddSingleton(_stockServiceMock.Object)
            .AddSingleton(_unitOfWorkMock.Object)
            .BuildServiceProvider()
            .GetRequiredService<IServiceScopeFactory>();
        var svc = new SeckillPreOccupationCompensationService(
            scopeFactory, new Mock<ILogger<SeckillPreOccupationCompensationService>>().Object);
        var method = typeof(SeckillPreOccupationCompensationService).GetMethod(
            "CompensateAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await (Task)method!.Invoke(svc, new object[] { CancellationToken.None })!;
    }

    private static SeckillPreOccupationRecord CreateRecord()
        => SeckillPreOccupationRecord.Create(ActivityId, SkuId, Guid.NewGuid(), Guid.NewGuid(), 1);
}
