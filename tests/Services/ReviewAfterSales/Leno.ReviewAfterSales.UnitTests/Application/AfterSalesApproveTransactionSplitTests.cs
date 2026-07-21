using Leno.ReviewAfterSales.Application.Services;
using Leno.ReviewAfterSales.Domain.Aggregates;
using Leno.ReviewAfterSales.Domain.Repositories;
using Leno.ReviewAfterSales.Domain.Services;
using Leno.ReviewAfterSales.Domain.ValueObjects;
using Leno.Infrastructure.Abstractions;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Leno.ReviewAfterSales.UnitTests.Application;

/// <summary>
/// 审计 3.7：ApproveAfterSalesAsync 在数据库事务内执行远程支付查询，长事务持锁。
/// 验证仅退款类型的事务拆分：第一次 SaveEntitiesAsync 在 payment 查询之前调用，
/// 远程查询发生在事务外，第二次 SaveEntitiesAsync 在 AddRefundRequestedEvent 之后调用。
/// </summary>
public sealed class AfterSalesApproveTransactionSplitTests
{
    private static AfterSales CreatePendingRefundOnly()
    {
        var sellerId = Guid.NewGuid();
        return AfterSales.Create(
            Guid.NewGuid(), Guid.NewGuid(), null,
            userId: Guid.NewGuid(), sellerId: sellerId,
            AfterSalesType.RefundOnly, "quality", "broken", null, 10m, "CNY");
    }

    private static (AfterSalesAppService svc, Mock<IUnitOfWork> uowMock, Mock<IPaymentInfoQueryService> paymentMock) CreateService(
        AfterSales afterSales,
        bool paymentReturnsNull = false)
    {
        var repoMock = new Mock<IAfterSalesRepository>();
        repoMock.Setup(r => r.GetByIdAsync(afterSales.Id, It.IsAny<CancellationToken>())).ReturnsAsync(afterSales);
        repoMock.Setup(r => r.UpdateAsync(It.IsAny<AfterSales>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var uowMock = new Mock<IUnitOfWork>();
        var saveCallCount = 0;
        uowMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                saveCallCount++;
                return Task.CompletedTask;
            })
            .Callback(() => { });

        var paymentMock = new Mock<IPaymentInfoQueryService>();
        if (!paymentReturnsNull)
        {
            paymentMock.Setup(p => p.GetByOrderIdAsync(afterSales.OrderId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PaymentInfoResult { PaymentId = Guid.NewGuid(), Channel = "WeChatPay" });
        }
        else
        {
            paymentMock.Setup(p => p.GetByOrderIdAsync(afterSales.OrderId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((PaymentInfoResult?)null);
        }

        var eligibilityMock = new Mock<IAfterSalesEligibilityChecker>();
        var orderProviderMock = new Mock<IOrderStatusProvider>();
        var eventBusMock = new Mock<IEventBus>();

        var svc = new AfterSalesAppService(
            repoMock.Object, eligibilityMock.Object, paymentMock.Object,
            orderProviderMock.Object, eventBusMock.Object, uowMock.Object,
            NullLogger<AfterSalesAppService>.Instance);
        return (svc, uowMock, paymentMock);
    }

    [Fact]
    public async Task ApproveAfterSalesAsync_RefundOnly_Should_Call_SaveEntities_Twice_And_PaymentQuery_Between()
    {
        var afterSales = CreatePendingRefundOnly();
        var (svc, uowMock, paymentMock) = CreateService(afterSales);

        var saveCallOrder = new List<string>();
        var saveCallCount = 0;
        uowMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                saveCallCount++;
                saveCallOrder.Add($"save{saveCallCount}");
                return Task.CompletedTask;
            });
        paymentMock.Setup(p => p.GetByOrderIdAsync(afterSales.OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentInfoResult { PaymentId = Guid.NewGuid(), Channel = "WeChatPay" })
            .Callback(() => saveCallOrder.Add("paymentQuery"));

        await svc.ApproveAfterSalesAsync(afterSales.Id, afterSales.SellerId, 10m);

        // 仅退款类型应该调用两次 SaveEntitiesAsync（拆分事务）
        Assert.Equal(2, saveCallCount);
        // 顺序：save1 → paymentQuery → save2（远程查询在第一次提交后，第二次提交前）
        Assert.Equal(new[] { "save1", "paymentQuery", "save2" }, saveCallOrder);
    }

    [Fact]
    public async Task ApproveAfterSalesAsync_ReturnRefund_Should_Call_SaveEntities_Once()
    {
        // 退货退款类型不触发退款流程，应只调用一次 SaveEntitiesAsync
        var sellerId = Guid.NewGuid();
        var afterSales = AfterSales.Create(
            Guid.NewGuid(), Guid.NewGuid(), null,
            userId: Guid.NewGuid(), sellerId: sellerId,
            AfterSalesType.ReturnRefund, "quality", "broken", null, 10m, "CNY");
        var (svc, uowMock, _) = CreateService(afterSales);

        var saveCallCount = 0;
        uowMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .Returns(() => { saveCallCount++; return Task.CompletedTask; });

        await svc.ApproveAfterSalesAsync(afterSales.Id, afterSales.SellerId, 10m);

        Assert.Equal(1, saveCallCount);
    }

    [Fact]
    public async Task ConfirmReturnAsync_Should_Call_SaveEntities_Twice_And_PaymentQuery_Between()
    {
        var sellerId = Guid.NewGuid();
        var afterSales = AfterSales.Create(
            Guid.NewGuid(), Guid.NewGuid(), null,
            userId: Guid.NewGuid(), sellerId: sellerId,
            AfterSalesType.ReturnRefund, "quality", "broken", null, 10m, "CNY");
        afterSales.Approve(sellerId, 10m);
        afterSales.ReturnGoods("TRACK001");
        var (svc, uowMock, paymentMock) = CreateService(afterSales);

        var saveCallOrder = new List<string>();
        var saveCallCount = 0;
        uowMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                saveCallCount++;
                saveCallOrder.Add($"save{saveCallCount}");
                return Task.CompletedTask;
            });
        paymentMock.Setup(p => p.GetByOrderIdAsync(afterSales.OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentInfoResult { PaymentId = Guid.NewGuid(), Channel = "WeChatPay" })
            .Callback(() => saveCallOrder.Add("paymentQuery"));

        await svc.ConfirmReturnAsync(afterSales.Id, afterSales.SellerId);

        Assert.Equal(2, saveCallCount);
        Assert.Equal(new[] { "save1", "paymentQuery", "save2" }, saveCallOrder);
    }
}
