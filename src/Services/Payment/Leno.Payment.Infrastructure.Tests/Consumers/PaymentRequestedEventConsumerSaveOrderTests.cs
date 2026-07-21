using System.Reflection;
using Leno.Infrastructure.Abstractions;
using Leno.Infrastructure.EventBus;
using Leno.Payment.Domain.Aggregates;
using Leno.Payment.Domain.Repositories;
using Leno.Payment.Domain.Services;
using Leno.Payment.Domain.ValueObjects;
using Leno.Payment.Infrastructure.Channels;
using Leno.Payment.Infrastructure.Consumers;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Leno.Payment.Infrastructure.Tests.Consumers;

/// <summary>
/// P0-6 测试：验证 PaymentRequestedEventConsumer 先持久化支付单（Pending 态）再调渠道下单。
/// 根因：原实现先调 <c>adapter.CreatePaymentAsync</c> 再 <c>AddAsync</c> + <c>SaveEntitiesAsync</c>，
/// 渠道下单成功但本地保存失败时支付单丢失，无法关联回调或对账，造成资金损失。
/// 修复后顺序：创建支付单 → AddAsync + SaveEntitiesAsync（Pending 态）→ CreatePaymentAsync →
/// MarkChannelOrdered/MarkFailed → UpdateAsync + SaveEntitiesAsync。
/// </summary>
public class PaymentRequestedEventConsumerSaveOrderTests
{
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private const decimal Amount = 100m;

    private static PaymentRequestedIntegrationEvent CreateEvent()
    {
        return new PaymentRequestedIntegrationEvent
        {
            EventId = Guid.NewGuid(),
            OrderId = OrderId,
            UserId = UserId,
            Amount = Amount,
            Currency = "CNY",
            Channel = "WeChatPay"
        };
    }

    /// <summary>
    /// 构造消费者，使用 Mock 的 <see cref="IPaymentChannelFactory"/>（P0-6 引入的抽象）。
    /// </summary>
    private static PaymentRequestedEventConsumer CreateConsumer(
        Mock<IPaymentOrderRepository> repoMock,
        Mock<IUnitOfWork> uowMock,
        Mock<IPaymentChannelFactory> factoryMock,
        Mock<IIdempotencyStore> idempotencyMock)
    {
        return new PaymentRequestedEventConsumer(
            repoMock.Object,
            uowMock.Object,
            factoryMock.Object,
            NullLogger<PaymentRequestedEventConsumer>.Instance,
            idempotencyMock.Object);
    }

    /// <summary>
    /// 通过反射调用受保护的 <see cref="IntegrationEventConsumerBase{T}.HandleAsync"/>，
    /// 与 Order 域 <c>PaymentSucceededEventConsumerTests</c> 同一约定。
    /// </summary>
    private static async Task InvokeHandleAsync(PaymentRequestedEventConsumer consumer, PaymentRequestedIntegrationEvent evt)
    {
        var handleMethod = typeof(PaymentRequestedEventConsumer)
            .GetMethod("HandleAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(handleMethod);
        await (Task)handleMethod!.Invoke(consumer, [evt, CancellationToken.None])!;
    }

    [Fact]
    public async Task HandleAsync_ShouldPersistPendingOrder_Before_CallingChannelAdapter()
    {
        // Arrange
        var repoMock = new Mock<IPaymentOrderRepository>();
        repoMock.Setup(r => r.GetByOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentOrder?)null);

        // 记录调用顺序与首次保存时支付单状态快照
        var callSequence = new List<string>();
        PaymentStatus? statusAtFirstSave = null;
        PaymentOrder? capturedOrder = null;

        repoMock.Setup(r => r.AddAsync(It.IsAny<PaymentOrder>(), It.IsAny<CancellationToken>()))
            .Callback<PaymentOrder, CancellationToken>((order, _) =>
            {
                capturedOrder = order;
                callSequence.Add("AddAsync");
            })
            .Returns(Task.CompletedTask);
        repoMock.Setup(r => r.UpdateAsync(It.IsAny<PaymentOrder>(), It.IsAny<CancellationToken>()))
            .Callback(() => callSequence.Add("UpdateAsync"))
            .Returns(Task.CompletedTask);

        var saveCallCount = 0;
        var uowMock = new Mock<IUnitOfWork>();
        uowMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                saveCallCount++;
                callSequence.Add("SaveEntitiesAsync");
                if (saveCallCount == 1)
                {
                    // 首次保存发生在渠道下单之前，支付单应为 Pending 态
                    statusAtFirstSave = capturedOrder?.Status;
                }
                return true;
            });

        var adapterMock = new Mock<IPaymentChannelAdapter>();
        adapterMock.Setup(a => a.CreatePaymentAsync(It.IsAny<PaymentOrder>(), It.IsAny<CancellationToken>()))
            .Callback(() => callSequence.Add("CreatePaymentAsync"))
            .ReturnsAsync(new ChannelPaymentResult
            {
                ChannelTradeNo = "4200000000202607220000000001",
                PrepayId = "wx001",
                CodeUrl = "weixin://wxpay/bizpayurl?pr=001"
            });

        var factoryMock = new Mock<IPaymentChannelFactory>();
        factoryMock.Setup(f => f.GetAdapter(PaymentChannel.WeChatPay))
            .Returns(adapterMock.Object);

        var idempotencyMock = new Mock<IIdempotencyStore>();

        var sut = CreateConsumer(repoMock, uowMock, factoryMock, idempotencyMock);

        // Act
        await InvokeHandleAsync(sut, CreateEvent());

        // Assert：首次 SaveEntitiesAsync 必须在 CreatePaymentAsync 之前
        Assert.Contains("AddAsync", callSequence);
        Assert.Contains("SaveEntitiesAsync", callSequence);
        Assert.Contains("CreatePaymentAsync", callSequence);

        var firstSaveIndex = callSequence.IndexOf("SaveEntitiesAsync");
        var createPaymentIndex = callSequence.IndexOf("CreatePaymentAsync");
        Assert.True(firstSaveIndex < createPaymentIndex,
            $"首次 SaveEntitiesAsync (index={firstSaveIndex}) 应在 CreatePaymentAsync (index={createPaymentIndex}) 之前");

        // 首次保存时支付单为 Pending 态（证明渠道下单前已持久化待支付单）
        Assert.NotNull(statusAtFirstSave);
        Assert.Equal(PaymentStatus.Pending, statusAtFirstSave.Value);

        // 渠道下单成功后应更新为 ChannelOrdered 并再次保存
        Assert.NotNull(capturedOrder);
        Assert.Equal(PaymentStatus.ChannelOrdered, capturedOrder!.Status);
        Assert.Contains("UpdateAsync", callSequence);
        Assert.Equal(2, saveCallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenChannelSucceedsButSecondSaveFails_ShouldHaveAlreadyPersistedPendingOrderAndRethrow()
    {
        // Arrange：首次保存（Pending 态）成功，渠道下单成功，第二次保存失败
        var repoMock = new Mock<IPaymentOrderRepository>();
        repoMock.Setup(r => r.GetByOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentOrder?)null);

        PaymentOrder? capturedOrder = null;
        repoMock.Setup(r => r.AddAsync(It.IsAny<PaymentOrder>(), It.IsAny<CancellationToken>()))
            .Callback<PaymentOrder, CancellationToken>((order, _) => capturedOrder = order)
            .Returns(Task.CompletedTask);

        var statusAtFirstSave = (PaymentStatus?)null;
        var saveCallCount = 0;
        var uowMock = new Mock<IUnitOfWork>();
        uowMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                saveCallCount++;
                if (saveCallCount == 1)
                {
                    // 首次保存发生在渠道下单之前，支付单应为 Pending 态
                    statusAtFirstSave = capturedOrder?.Status;
                    return true;
                }
                // 第二次保存（更新渠道已下单态）失败，模拟 DB 连接断开
                throw new InvalidOperationException("DB connection lost");
            });

        var adapterMock = new Mock<IPaymentChannelAdapter>();
        adapterMock.Setup(a => a.CreatePaymentAsync(It.IsAny<PaymentOrder>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelPaymentResult
            {
                ChannelTradeNo = "4200000000202607220000000001",
                PrepayId = "wx001",
                CodeUrl = "weixin://wxpay/bizpayurl?pr=001"
            });

        var factoryMock = new Mock<IPaymentChannelFactory>();
        factoryMock.Setup(f => f.GetAdapter(PaymentChannel.WeChatPay))
            .Returns(adapterMock.Object);

        var idempotencyMock = new Mock<IIdempotencyStore>();

        var sut = CreateConsumer(repoMock, uowMock, factoryMock, idempotencyMock);

        // Act：第二次保存失败，异常应向上抛出触发消息重试；但支付单已通过首次保存持久化
        var ex = await Record.ExceptionAsync(() => InvokeHandleAsync(sut, CreateEvent()));

        // Assert：支付单已被首次保存到仓储（Pending 态），异常向上抛出
        Assert.NotNull(capturedOrder);
        Assert.NotNull(statusAtFirstSave);
        Assert.Equal(PaymentStatus.Pending, statusAtFirstSave!.Value);
        Assert.Equal(2, saveCallCount);
        Assert.NotNull(ex);
        Assert.IsType<InvalidOperationException>(ex);
        repoMock.Verify(r => r.AddAsync(It.IsAny<PaymentOrder>(), It.IsAny<CancellationToken>()), Times.Once);
        repoMock.Verify(r => r.UpdateAsync(It.IsAny<PaymentOrder>(), It.IsAny<CancellationToken>()), Times.Once);
        adapterMock.Verify(a => a.CreatePaymentAsync(It.IsAny<PaymentOrder>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenChannelReturnsEmptyTradeNo_ShouldMarkFailed_AndPersistOrder()
    {
        // Arrange：渠道下单未返回交易号，应标记失败并保存，支付单已被首次持久化
        var repoMock = new Mock<IPaymentOrderRepository>();
        repoMock.Setup(r => r.GetByOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentOrder?)null);

        PaymentOrder? capturedOrder = null;
        repoMock.Setup(r => r.AddAsync(It.IsAny<PaymentOrder>(), It.IsAny<CancellationToken>()))
            .Callback<PaymentOrder, CancellationToken>((order, _) => capturedOrder = order)
            .Returns(Task.CompletedTask);

        var saveCallCount = 0;
        var uowMock = new Mock<IUnitOfWork>();
        uowMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                saveCallCount++;
                return true;
            });

        var adapterMock = new Mock<IPaymentChannelAdapter>();
        adapterMock.Setup(a => a.CreatePaymentAsync(It.IsAny<PaymentOrder>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelPaymentResult
            {
                ChannelTradeNo = null,
                PrepayId = null,
                CodeUrl = null
            });

        var factoryMock = new Mock<IPaymentChannelFactory>();
        factoryMock.Setup(f => f.GetAdapter(PaymentChannel.WeChatPay))
            .Returns(adapterMock.Object);

        var idempotencyMock = new Mock<IIdempotencyStore>();

        var sut = CreateConsumer(repoMock, uowMock, factoryMock, idempotencyMock);

        // Act
        await InvokeHandleAsync(sut, CreateEvent());

        // Assert：支付单先以 Pending 持久化，渠道下单失败后标记 Failed 并再次保存
        Assert.NotNull(capturedOrder);
        Assert.Equal(PaymentStatus.Failed, capturedOrder!.Status);
        Assert.Equal(2, saveCallCount);
        repoMock.Verify(r => r.AddAsync(It.IsAny<PaymentOrder>(), It.IsAny<CancellationToken>()), Times.Once);
        repoMock.Verify(r => r.UpdateAsync(It.IsAny<PaymentOrder>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
