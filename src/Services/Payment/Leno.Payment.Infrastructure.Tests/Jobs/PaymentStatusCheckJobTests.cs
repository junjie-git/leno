using Leno.Payment.Domain.Aggregates;
using Leno.Payment.Domain.Repositories;
using Leno.Payment.Domain.Services;
using Leno.Payment.Domain.ValueObjects;
using Leno.Payment.Infrastructure.Channels;
using Leno.Payment.Infrastructure.Jobs;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Leno.Payment.Infrastructure.Tests.Jobs;

/// <summary>
/// P1-11 测试：验证 PaymentStatusCheckJob 对 ExpireAt 已过期的 Pending/ChannelOrdered 态支付单主动关单。
/// 根因：原实现仅查询 Pending/ChannelOrdered 态并调用渠道查询接口，未检查 ExpireAt 字段。
/// 过期支付单会一直堆积并被反复查询渠道，浪费资源且可能被渠道侧拒绝。
/// 修复后：ExecuteAsync 末尾调用 CloseExpiredOrdersAsync 扫描 ExpireAt 已过期的支付单并 MarkClosed。
/// </summary>
public class PaymentStatusCheckJobTests
{
    /// <summary>
    /// 通过反射设置 PaymentOrder.ExpireAt（private set）为指定时间。
    /// </summary>
    private static void SetExpireAt(PaymentOrder order, DateTime expireAt)
    {
        typeof(PaymentOrder)
            .GetProperty("ExpireAt")!
            .SetValue(order, expireAt);
    }

    private static PaymentOrder CreatePendingOrder(DateTime expireAt)
    {
        var order = PaymentOrder.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            100m, "CNY", PaymentChannel.WeChatPay);
        SetExpireAt(order, expireAt);
        return order;
    }

    private static PaymentOrder CreateChannelOrderedOrder(DateTime expireAt)
    {
        var order = PaymentOrder.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            100m, "CNY", PaymentChannel.WeChatPay);
        order.MarkChannelOrdered("WX_TXN_TEST_001", null, null, null);
        SetExpireAt(order, expireAt);
        return order;
    }

    private static PaymentStatusCheckJob CreateJob(
        Mock<IPaymentOrderRepository> repoMock,
        Mock<IUnitOfWork> uowMock,
        Mock<IPaymentChannelFactory> factoryMock)
    {
        return new PaymentStatusCheckJob(
            repoMock.Object,
            uowMock.Object,
            factoryMock.Object,
            NullLogger<PaymentStatusCheckJob>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_WhenExpiredPendingOrderExists_ShouldMarkClosed()
    {
        // 安排：ExpireAt 已过期的 Pending 支付单
        var expiredOrder = CreatePendingOrder(DateTime.UtcNow.AddHours(-1));

        var repoMock = new Mock<IPaymentOrderRepository>();
        // QueryAsync 返回空（避免触发渠道查询分支）
        repoMock
            .Setup(r => r.QueryAsync(
                It.IsAny<Guid?>(), It.IsAny<PaymentChannel?>(), It.IsAny<PaymentStatus?>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PaymentOrder>());

        // GetExpiredOrdersAsync 第一页返回过期单，第二页返回空
        repoMock
            .SetupSequence(r => r.GetExpiredOrdersAsync(
                It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PaymentOrder> { expiredOrder })
            .ReturnsAsync(new List<PaymentOrder>());

        var uowMock = new Mock<IUnitOfWork>();
        uowMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var adapterMock = new Mock<IPaymentChannelAdapter>();
        var factoryMock = new Mock<IPaymentChannelFactory>();
        factoryMock.Setup(f => f.GetAdapter(It.IsAny<PaymentChannel>())).Returns(adapterMock.Object);

        var sut = CreateJob(repoMock, uowMock, factoryMock);

        // 行动
        await sut.ExecuteAsync(CancellationToken.None);

        // 断言：过期支付单被关单
        Assert.Equal(PaymentStatus.Closed, expiredOrder.Status);
        repoMock.Verify(r => r.UpdateAsync(expiredOrder, It.IsAny<CancellationToken>()), Times.Once);
        uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenExpiredChannelOrderedOrderExists_ShouldMarkClosed()
    {
        // 安排：ExpireAt 已过期的 ChannelOrdered 支付单
        var expiredOrder = CreateChannelOrderedOrder(DateTime.UtcNow.AddHours(-1));

        var repoMock = new Mock<IPaymentOrderRepository>();
        repoMock
            .Setup(r => r.QueryAsync(
                It.IsAny<Guid?>(), It.IsAny<PaymentChannel?>(), It.IsAny<PaymentStatus?>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PaymentOrder>());

        repoMock
            .SetupSequence(r => r.GetExpiredOrdersAsync(
                It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PaymentOrder> { expiredOrder })
            .ReturnsAsync(new List<PaymentOrder>());

        var uowMock = new Mock<IUnitOfWork>();
        uowMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var adapterMock = new Mock<IPaymentChannelAdapter>();
        var factoryMock = new Mock<IPaymentChannelFactory>();
        factoryMock.Setup(f => f.GetAdapter(It.IsAny<PaymentChannel>())).Returns(adapterMock.Object);

        var sut = CreateJob(repoMock, uowMock, factoryMock);

        // 行动
        await sut.ExecuteAsync(CancellationToken.None);

        // 断言：过期 ChannelOrdered 支付单被关单
        Assert.Equal(PaymentStatus.Closed, expiredOrder.Status);
        repoMock.Verify(r => r.UpdateAsync(expiredOrder, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoExpiredOrder_ShouldNotCallUpdateOrSave()
    {
        // 安排：无过期支付单
        var repoMock = new Mock<IPaymentOrderRepository>();
        repoMock
            .Setup(r => r.QueryAsync(
                It.IsAny<Guid?>(), It.IsAny<PaymentChannel?>(), It.IsAny<PaymentStatus?>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PaymentOrder>());

        repoMock
            .Setup(r => r.GetExpiredOrdersAsync(
                It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PaymentOrder>());

        var uowMock = new Mock<IUnitOfWork>();
        var adapterMock = new Mock<IPaymentChannelAdapter>();
        var factoryMock = new Mock<IPaymentChannelFactory>();
        factoryMock.Setup(f => f.GetAdapter(It.IsAny<PaymentChannel>())).Returns(adapterMock.Object);

        var sut = CreateJob(repoMock, uowMock, factoryMock);

        // 行动
        await sut.ExecuteAsync(CancellationToken.None);

        // 断言：无过期单，不调用 UpdateAsync，关单分支不调用 SaveEntitiesAsync
        // 注意：SaveEntitiesAsync 可能被 CheckAsync 调用，但本场景 QueryAsync 返回空，CheckAsync 不会触发
        repoMock.Verify(r => r.UpdateAsync(It.IsAny<PaymentOrder>(), It.IsAny<CancellationToken>()), Times.Never);
        uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPaginateExpiredOrders_UntilEmptyPage()
    {
        // 安排：多页过期支付单，验证分页循环
        var expiredBatch1 = new List<PaymentOrder>
        {
            CreatePendingOrder(DateTime.UtcNow.AddHours(-1)),
            CreatePendingOrder(DateTime.UtcNow.AddHours(-2))
        };
        var expiredBatch2 = new List<PaymentOrder>
        {
            CreatePendingOrder(DateTime.UtcNow.AddHours(-3))
        };

        var repoMock = new Mock<IPaymentOrderRepository>();
        repoMock
            .Setup(r => r.QueryAsync(
                It.IsAny<Guid?>(), It.IsAny<PaymentChannel?>(), It.IsAny<PaymentStatus?>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PaymentOrder>());

        // 第 1 页返回 2 条（< BatchSize=100，触发退出），不再返回第 2 页
        repoMock
            .SetupSequence(r => r.GetExpiredOrdersAsync(
                It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expiredBatch1)
            .ReturnsAsync(expiredBatch2)
            .ReturnsAsync(new List<PaymentOrder>());

        var uowMock = new Mock<IUnitOfWork>();
        uowMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var adapterMock = new Mock<IPaymentChannelAdapter>();
        var factoryMock = new Mock<IPaymentChannelFactory>();
        factoryMock.Setup(f => f.GetAdapter(It.IsAny<PaymentChannel>())).Returns(adapterMock.Object);

        var sut = CreateJob(repoMock, uowMock, factoryMock);

        // 行动
        await sut.ExecuteAsync(CancellationToken.None);

        // 断言：所有过期支付单均被关单
        Assert.All(expiredBatch1.Concat(expiredBatch2), o => Assert.Equal(PaymentStatus.Closed, o.Status));
        repoMock.Verify(
            r => r.UpdateAsync(It.IsAny<PaymentOrder>(), It.IsAny<CancellationToken>()),
            Times.Exactly(expiredBatch1.Count + expiredBatch2.Count));
    }
}
