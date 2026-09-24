using Leno.Infrastructure.Abstractions;
using Leno.Inventory.Application.DTOs;
using Leno.Inventory.Application.Services;
using Leno.Inventory.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Leno.Inventory.Application.Tests.Services;

/// <summary>
/// SeckillStockAppService 单元测试。
/// 重点覆盖 RestoreAsync 的幂等与原子处理权获取（P1 修复：check-then-act 竞态消除），
/// 以及 TryDeductAsync 的返回码映射。
/// </summary>
public class SeckillStockAppServiceTests
{
    private readonly Mock<ISeckillStockService> _seckillStockServiceMock = new();
    private readonly Mock<IIdempotencyStore> _idempotencyStoreMock = new();
    private readonly SeckillStockAppService _sut;

    public SeckillStockAppServiceTests()
    {
        _idempotencyStoreMock.SetupGet(s => s.SupportsAtomicProcessing).Returns(true);
        _idempotencyStoreMock.Setup(s => s.TryMarkAsProcessingAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _sut = new SeckillStockAppService(
            _seckillStockServiceMock.Object,
            _idempotencyStoreMock.Object,
            Mock.Of<ILogger<SeckillStockAppService>>());
    }

    #region TryDeductAsync（返回码映射）

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(99, false)]
    public async Task TryDeductAsync_Should_Map_Redis_Return_Code(int code, bool expectedSuccess)
    {
        _seckillStockServiceMock
            .Setup(s => s.TryDeductAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(code);

        var result = await _sut.TryDeductAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, 2);

        result.IsSuccess.Should().Be(expectedSuccess);
        result.Code.Should().Be(code);
        if (!expectedSuccess)
        {
            result.FailureReason.Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public async Task TryDeductAsync_Success_Should_Not_Set_FailureReason()
    {
        _seckillStockServiceMock
            .Setup(s => s.TryDeductAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var result = await _sut.TryDeductAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, 2);

        result.Should().BeOfType<SeckillDeductResult>();
        result.Code.Should().Be(0);
        result.FailureReason.Should().BeNull();
    }

    #endregion

    #region RestoreAsync（幂等 + 原子处理权，P1#12 修复验证）

    [Fact]
    public async Task RestoreAsync_With_Empty_Key_Should_Restore_Directly_Without_Idempotency()
    {
        var activityId = Guid.NewGuid();

        await _sut.RestoreAsync(activityId, Guid.NewGuid(), 1, Guid.Empty);

        _seckillStockServiceMock.Verify(
            s => s.RestoreAsync(activityId, It.IsAny<Guid>(), 1, It.IsAny<CancellationToken>()), Times.Once,
            "未提供幂等键时保持向后兼容：直接执行，由底层 TotalStock 上限保护");
        _idempotencyStoreMock.Verify(
            s => s.IsProcessedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RestoreAsync_When_Already_Processed_Should_Skip_Restore()
    {
        _idempotencyStoreMock
            .Setup(s => s.IsProcessedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var activityId = Guid.NewGuid();

        await _sut.RestoreAsync(activityId, Guid.NewGuid(), 1, Guid.NewGuid());

        _seckillStockServiceMock.Verify(
            s => s.RestoreAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never, "已处理的幂等键不得重复回退");
        _idempotencyStoreMock.Verify(
            s => s.TryMarkAsProcessingAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RestoreAsync_With_Atomic_Store_Should_TryMark_Restore_Mark()
    {
        var activityId = Guid.NewGuid();
        var skuId = Guid.NewGuid();
        var key = Guid.NewGuid();
        var callOrder = new List<string>();
        _idempotencyStoreMock
            .Setup(s => s.TryMarkAsProcessingAsync(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .Callback(() => callOrder.Add("tryMark"));
        _seckillStockServiceMock
            .Setup(s => s.RestoreAsync(activityId, skuId, 1, It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("restore"))
            .Returns(Task.CompletedTask);
        _idempotencyStoreMock
            .Setup(s => s.MarkAsProcessedAsync(key, It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("mark"))
            .Returns(Task.CompletedTask);

        await _sut.RestoreAsync(activityId, skuId, 1, key);

        // 必须先原子占锁再回退，最后标记完成
        callOrder.Should().Equal("tryMark", "restore", "mark");
    }

    [Fact]
    public async Task RestoreAsync_When_Lost_Atomic_Race_Should_Skip_Restore_And_Mark()
    {
        _idempotencyStoreMock
            .Setup(s => s.IsProcessedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _idempotencyStoreMock
            .Setup(s => s.TryMarkAsProcessingAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await _sut.RestoreAsync(Guid.NewGuid(), Guid.NewGuid(), 1, Guid.NewGuid());

        _seckillStockServiceMock.Verify(
            s => s.RestoreAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never, "并发竞争落败方不得回退，防止双重回退");
        _idempotencyStoreMock.Verify(
            s => s.MarkAsProcessedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RestoreAsync_When_Restore_Fails_Should_Release_Lock_And_Rethrow()
    {
        _idempotencyStoreMock
            .Setup(s => s.IsProcessedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _seckillStockServiceMock
            .Setup(s => s.RestoreAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("redis down"));

        var act = async () => await _sut.RestoreAsync(Guid.NewGuid(), Guid.NewGuid(), 1, Guid.NewGuid());

        await act.Should().ThrowAsync<InvalidOperationException>();
        _idempotencyStoreMock.Verify(
            s => s.ReleaseProcessingLockAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once,
            "失败必须释放处理锁，允许上游重试");
        _idempotencyStoreMock.Verify(
            s => s.MarkAsProcessedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RestoreAsync_With_NonAtomic_Store_Should_Fall_Back_To_Legacy_Path()
    {
        _idempotencyStoreMock.SetupGet(s => s.SupportsAtomicProcessing).Returns(false);
        _idempotencyStoreMock
            .Setup(s => s.IsProcessedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var activityId = Guid.NewGuid();

        await _sut.RestoreAsync(activityId, Guid.NewGuid(), 1, Guid.NewGuid());

        _seckillStockServiceMock.Verify(
            s => s.RestoreAsync(activityId, It.IsAny<Guid>(), 1, It.IsAny<CancellationToken>()), Times.Once);
        _idempotencyStoreMock.Verify(
            s => s.TryMarkAsProcessingAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never,
            "不支持的实现走 IsProcessed → Restore → Mark 旧三步流程");
        _idempotencyStoreMock.Verify(
            s => s.MarkAsProcessedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region 查询与初始化透传

    [Fact]
    public async Task GetAvailableAsync_Should_Delegate_To_Redis_Service()
    {
        _seckillStockServiceMock
            .Setup(s => s.GetAvailableAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);
        var activityId = Guid.NewGuid();
        var skuId = Guid.NewGuid();

        var available = await _sut.GetAvailableAsync(activityId, skuId);

        available.Should().Be(42);
    }

    [Fact]
    public async Task InitializeAsync_Should_Delegate_To_Redis_Service()
    {
        var activityId = Guid.NewGuid();
        var skuStocks = new Dictionary<Guid, int> { [Guid.NewGuid()] = 10 };

        await _sut.InitializeAsync(activityId, skuStocks);

        _seckillStockServiceMock.Verify(
            s => s.InitializeAsync(activityId, skuStocks, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InitializeAsync_With_Null_Stocks_Should_Throw_ArgumentNullException()
    {
        var initialize = async () => await _sut.InitializeAsync(Guid.NewGuid(), null!);

        await initialize.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion
}
