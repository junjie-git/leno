using Leno.Order.Application.ProcessManagers;
using Leno.Order.Application.ProcessManagers.Commands;
using Leno.Order.Application.ProcessManagers.Events;
using Leno.Order.Application.ProcessManagers.States;
using Leno.SharedKernel.Abstractions;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;

namespace Leno.Order.Application.Tests.ProcessManagers;

/// <summary>
/// <see cref="OrderPaymentProcessManager"/> 单元测试。
/// 验证 Process Manager 的状态创建、子任务回调、完成判定与反向补偿逻辑。
/// </summary>
public class OrderPaymentProcessManagerTests
{
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid PaymentId = Guid.NewGuid();
    private static readonly Guid ProcessId = Guid.NewGuid();

    private readonly Mock<IOrderPaymentProcessRepository> _repositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IBus> _busMock = new();
    private readonly Mock<ILogger<OrderPaymentProcessManager>> _loggerMock = new();

    public OrderPaymentProcessManagerTests()
    {
        _unitOfWorkMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _busMock.Setup(b => b.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private OrderPaymentProcessManager CreateSut() => new(
        _repositoryMock.Object,
        _unitOfWorkMock.Object,
        _busMock.Object,
        _loggerMock.Object);

    [Fact]
    public async Task StartAsync_Should_Create_State_And_Publish_Events_And_Commands()
    {
        // Arrange
        _repositoryMock.Setup(r => r.GetByOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrderPaymentProcessState?)null);

        var sut = CreateSut();

        // Act
        var state = await sut.StartAsync(
            OrderId, PaymentId, "alipay", "2024071200001", 99.99m, "CNY", DateTime.UtcNow);

        // Assert
        state.Should().NotBeNull();
        state.OrderId.Should().Be(OrderId);
        state.PaymentId.Should().Be(PaymentId);
        state.ProcessId.Should().NotBe(Guid.Empty);
        state.CurrentState.Should().Be(OrderPaymentProcessManager.StateNames.AwaitingStockConfirm);
        state.StockConfirmed.Should().BeFalse();
        state.PointsConfirmed.Should().BeFalse();
        state.OrderMarkedPaid.Should().BeFalse();

        _repositoryMock.Verify(r => r.SaveAsync(state, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);

        // 应发布编排启动事件 + 三个子任务命令
        _busMock.Verify(b => b.Publish(It.IsAny<OrderPaymentProcessStarted>(), It.IsAny<CancellationToken>()), Times.Once);
        _busMock.Verify(b => b.Publish(It.IsAny<ConfirmStockCommand>(), It.IsAny<CancellationToken>()), Times.Once);
        _busMock.Verify(b => b.Publish(It.IsAny<ConfirmPointsCommand>(), It.IsAny<CancellationToken>()), Times.Once);
        _busMock.Verify(b => b.Publish(It.IsAny<MarkOrderPaidCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StartAsync_When_State_Already_Exists_Should_Return_Existing_Without_Republishing()
    {
        // Arrange
        var existingState = CreateExistingState();
        _repositoryMock.Setup(r => r.GetByOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingState);

        var sut = CreateSut();

        // Act
        var state = await sut.StartAsync(
            OrderId, PaymentId, "alipay", "2024071200001", 99.99m, "CNY", DateTime.UtcNow);

        // Assert
        state.Should().BeSameAs(existingState);
        _repositoryMock.Verify(r => r.SaveAsync(It.IsAny<OrderPaymentProcessState>(), It.IsAny<CancellationToken>()), Times.Never);
        _busMock.Verify(b => b.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StartAsync_With_Empty_OrderId_Should_Throw()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var act = async () => await sut.StartAsync(
            Guid.Empty, PaymentId, "alipay", "2024071200001", 99.99m, "CNY", DateTime.UtcNow);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task StartAsync_With_Empty_PaymentId_Should_Throw()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var act = async () => await sut.StartAsync(
            OrderId, Guid.Empty, "alipay", "2024071200001", 99.99m, "CNY", DateTime.UtcNow);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task HandleStockConfirmedAsync_When_State_Not_Found_Should_Skip()
    {
        // Arrange
        _repositoryMock.Setup(r => r.GetByOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrderPaymentProcessState?)null);
        var sut = CreateSut();

        // Act
        await sut.HandleStockConfirmedAsync(OrderId);

        // Assert
        _repositoryMock.Verify(r => r.SaveAsync(It.IsAny<OrderPaymentProcessState>(), It.IsAny<CancellationToken>()), Times.Never);
        _busMock.Verify(b => b.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleStockConfirmedAsync_When_Already_Confirmed_Should_Be_Idempotent()
    {
        // Arrange
        var state = CreateExistingState();
        state.StockConfirmed = true;
        _repositoryMock.Setup(r => r.GetByOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(state);
        var sut = CreateSut();

        // Act
        await sut.HandleStockConfirmedAsync(OrderId);

        // Assert
        _repositoryMock.Verify(r => r.SaveAsync(It.IsAny<OrderPaymentProcessState>(), It.IsAny<CancellationToken>()), Times.Never);
        _busMock.Verify(b => b.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleStockConfirmedAsync_When_Terminal_State_Should_Skip()
    {
        // Arrange
        var state = CreateExistingState();
        state.CurrentState = OrderPaymentProcessManager.StateNames.Completed;
        _repositoryMock.Setup(r => r.GetByOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(state);
        var sut = CreateSut();

        // Act
        await sut.HandleStockConfirmedAsync(OrderId);

        // Assert
        state.StockConfirmed.Should().BeFalse();
        _repositoryMock.Verify(r => r.SaveAsync(It.IsAny<OrderPaymentProcessState>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TryCompleteAsync_When_All_Three_Flags_Set_Should_Complete_And_Publish_Completed_Event()
    {
        // Arrange
        var state = CreateExistingState();
        state.StockConfirmed = true;
        state.PointsConfirmed = true;
        state.OrderMarkedPaid = true;
        var sut = CreateSut();

        // Act
        await sut.TryCompleteAsync(state);

        // Assert
        state.CurrentState.Should().Be(OrderPaymentProcessManager.StateNames.Completed);
        _repositoryMock.Verify(r => r.SaveAsync(state, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _busMock.Verify(b => b.Publish(It.IsAny<OrderPaymentProcessCompleted>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TryCompleteAsync_When_Not_All_Flags_Set_Should_Update_Intermediate_State()
    {
        // Arrange
        var state = CreateExistingState();
        state.StockConfirmed = true;
        state.PointsConfirmed = false;
        state.OrderMarkedPaid = false;
        var sut = CreateSut();

        // Act
        await sut.TryCompleteAsync(state);

        // Assert
        state.CurrentState.Should().Be(OrderPaymentProcessManager.StateNames.AwaitingPointsConfirm);
        _repositoryMock.Verify(r => r.SaveAsync(state, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _busMock.Verify(b => b.Publish(It.IsAny<OrderPaymentProcessCompleted>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleStockConfirmedAsync_Should_Set_Flag_And_Call_TryComplete()
    {
        // Arrange
        var state = CreateExistingState();
        _repositoryMock.Setup(r => r.GetByOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(state);
        var sut = CreateSut();

        // Act
        await sut.HandleStockConfirmedAsync(OrderId);

        // Assert
        state.StockConfirmed.Should().BeTrue();
        state.CurrentState.Should().Be(OrderPaymentProcessManager.StateNames.AwaitingPointsConfirm);
        _repositoryMock.Verify(r => r.SaveAsync(state, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandlePointsConfirmedAsync_Should_Set_Flag_And_Call_TryComplete()
    {
        // Arrange
        var state = CreateExistingState();
        _repositoryMock.Setup(r => r.GetByOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(state);
        var sut = CreateSut();

        // Act
        await sut.HandlePointsConfirmedAsync(OrderId);

        // Assert
        state.PointsConfirmed.Should().BeTrue();
        _repositoryMock.Verify(r => r.SaveAsync(state, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleOrderMarkedPaidAsync_Should_Set_Flag_And_Call_TryComplete()
    {
        // Arrange
        var state = CreateExistingState();
        _repositoryMock.Setup(r => r.GetByOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(state);
        var sut = CreateSut();

        // Act
        await sut.HandleOrderMarkedPaidAsync(OrderId);

        // Assert
        state.OrderMarkedPaid.Should().BeTrue();
        _repositoryMock.Verify(r => r.SaveAsync(state, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleSubTaskFailedAsync_Should_Enter_Compensating_And_Publish_Compensation_Commands()
    {
        // Arrange
        var state = CreateExistingState();
        state.StockConfirmed = true;
        state.PointsConfirmed = false;
        state.OrderMarkedPaid = true;
        _repositoryMock.Setup(r => r.GetByOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(state);
        var sut = CreateSut();

        // Act
        await sut.HandleSubTaskFailedAsync(OrderId, OrderPaymentProcessManager.SubTaskNames.Points);

        // Assert
        state.CurrentState.Should().Be(OrderPaymentProcessManager.StateNames.Compensated);

        // 应对已完成的子任务发布反向补偿命令（Stock 和 MarkOrderPaid 已完成，Points 未完成不补偿）
        _busMock.Verify(b => b.Publish(It.IsAny<OrderPaymentProcessCompensating>(), It.IsAny<CancellationToken>()), Times.Once);
        _busMock.Verify(b => b.Publish(It.IsAny<CompensateStockConfirmCommand>(), It.IsAny<CancellationToken>()), Times.Once);
        _busMock.Verify(b => b.Publish(It.IsAny<CompensateMarkOrderPaidCommand>(), It.IsAny<CancellationToken>()), Times.Once);
        _busMock.Verify(b => b.Publish(It.IsAny<CompensatePointsConfirmCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleSubTaskFailedAsync_When_Already_Terminal_Should_Skip()
    {
        // Arrange
        var state = CreateExistingState();
        state.CurrentState = OrderPaymentProcessManager.StateNames.Completed;
        _repositoryMock.Setup(r => r.GetByOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(state);
        var sut = CreateSut();

        // Act
        await sut.HandleSubTaskFailedAsync(OrderId, OrderPaymentProcessManager.SubTaskNames.Stock);

        // Assert
        _repositoryMock.Verify(r => r.SaveAsync(It.IsAny<OrderPaymentProcessState>(), It.IsAny<CancellationToken>()), Times.Never);
        _busMock.Verify(b => b.Publish(It.IsAny<OrderPaymentProcessCompensating>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleSubTaskFailedAsync_When_State_Not_Found_Should_Skip()
    {
        // Arrange
        _repositoryMock.Setup(r => r.GetByOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrderPaymentProcessState?)null);
        var sut = CreateSut();

        // Act
        await sut.HandleSubTaskFailedAsync(OrderId, OrderPaymentProcessManager.SubTaskNames.Stock);

        // Assert
        _repositoryMock.Verify(r => r.SaveAsync(It.IsAny<OrderPaymentProcessState>(), It.IsAny<CancellationToken>()), Times.Never);
        _busMock.Verify(b => b.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Full_Flow_All_Three_Callbacks_Should_Complete()
    {
        // Arrange：模拟三个子任务按乱序完成
        var state = CreateExistingState();
        _repositoryMock.Setup(r => r.GetByOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(state);
        var sut = CreateSut();

        // Act：按 Points → Stock → MarkOrderPaid 顺序完成
        await sut.HandlePointsConfirmedAsync(OrderId);
        state.CurrentState.Should().Be(OrderPaymentProcessManager.StateNames.AwaitingPointsConfirm);

        await sut.HandleStockConfirmedAsync(OrderId);
        state.CurrentState.Should().Be(OrderPaymentProcessManager.StateNames.AwaitingMarkPaid);

        await sut.HandleOrderMarkedPaidAsync(OrderId);

        // Assert
        state.CurrentState.Should().Be(OrderPaymentProcessManager.StateNames.Completed);
        _busMock.Verify(b => b.Publish(It.IsAny<OrderPaymentProcessCompleted>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static OrderPaymentProcessState CreateExistingState() => new()
    {
        ProcessId = ProcessId,
        OrderId = OrderId,
        PaymentId = PaymentId,
        CurrentState = OrderPaymentProcessManager.StateNames.AwaitingStockConfirm,
        StockConfirmed = false,
        PointsConfirmed = false,
        OrderMarkedPaid = false,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        RowVersion = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }
    };
}

/// <summary>
/// <see cref="OrderPaymentProcessRolloutEvaluator"/> 单元测试。
/// 验证灰度切流评估器的全局开关与百分比哈希逻辑。
/// </summary>
public class OrderPaymentProcessRolloutEvaluatorTests
{
    [Fact]
    public void ShouldUseProcessManager_When_Global_Switch_Off_Should_Return_False()
    {
        var options = new OrderPaymentProcessOptions { UsePaymentProcessManager = false, RolloutPercent = 100 };
        OrderPaymentProcessRolloutEvaluator.ShouldUseProcessManager(options, Guid.NewGuid())
            .Should().BeFalse();
    }

    [Fact]
    public void ShouldUseProcessManager_When_Switch_On_And_Rollout_100_Should_Return_True()
    {
        var options = new OrderPaymentProcessOptions { UsePaymentProcessManager = true, RolloutPercent = 100 };
        OrderPaymentProcessRolloutEvaluator.ShouldUseProcessManager(options, Guid.NewGuid())
            .Should().BeTrue();
    }

    [Fact]
    public void ShouldUseProcessManager_When_Switch_On_And_Rollout_0_Should_Return_False()
    {
        var options = new OrderPaymentProcessOptions { UsePaymentProcessManager = true, RolloutPercent = 0 };
        OrderPaymentProcessRolloutEvaluator.ShouldUseProcessManager(options, Guid.NewGuid())
            .Should().BeFalse();
    }

    [Fact]
    public void ShouldUseProcessManager_Should_Be_Stable_For_Same_OrderId()
    {
        var options = new OrderPaymentProcessOptions { UsePaymentProcessManager = true, RolloutPercent = 50 };
        var orderId = Guid.NewGuid();

        var first = OrderPaymentProcessRolloutEvaluator.ShouldUseProcessManager(options, orderId);
        var second = OrderPaymentProcessRolloutEvaluator.ShouldUseProcessManager(options, orderId);

        first.Should().Be(second);
    }

    [Fact]
    public void ShouldUseProcessManager_With_Null_Options_Should_Throw()
    {
        var act = () => OrderPaymentProcessRolloutEvaluator.ShouldUseProcessManager(null!, Guid.NewGuid());
        act.Should().Throw<ArgumentNullException>();
    }
}
