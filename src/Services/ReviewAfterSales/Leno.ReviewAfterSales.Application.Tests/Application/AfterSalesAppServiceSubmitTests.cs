using Leno.Infrastructure.Abstractions;
using Leno.ReviewAfterSales.Application.DTOs;
using Leno.ReviewAfterSales.Application.Services;
using Leno.ReviewAfterSales.Domain.Aggregates;
using Leno.ReviewAfterSales.Domain.Repositories;
using Leno.ReviewAfterSales.Domain.Services;
using Leno.ReviewAfterSales.Domain.ValueObjects;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using AfterSalesAggregate = Leno.ReviewAfterSales.Domain.Aggregates.AfterSales;

namespace Leno.ReviewAfterSales.Application.Tests.Application;

/// <summary>
/// 售后申请提交安全测试，验证 SubmitAfterSalesAsync 忽略 dto.SellerId，
/// 使用资格校验器返回的 OrderStatusInfo.SellerId 创建售后单聚合，防止客户端伪造卖家标识。
/// </summary>
public sealed class AfterSalesAppServiceSubmitTests
{
    private static readonly Guid RealSellerId = Guid.NewGuid();

    [Fact]
    public async Task SubmitAfterSalesAsync_Should_Ignore_Dto_SellerId_And_Use_Order_SellerId()
    {
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var forgedSellerId = Guid.NewGuid();
        Guid? capturedSellerId = null;

        var eligibilityMock = new Mock<IAfterSalesEligibilityChecker>();
        eligibilityMock
            .Setup(c => c.EnsureEligibleAsync(orderId, It.IsAny<Guid?>(), userId, It.IsAny<AfterSalesType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderStatusInfo
            {
                OrderId = orderId,
                Status = 2,
                UserId = userId,
                SellerId = RealSellerId,
                Items = new List<OrderItemStatusInfo>()
            });

        var repoMock = new Mock<IAfterSalesRepository>();
        repoMock
            .Setup(r => r.AddAsync(It.IsAny<AfterSalesAggregate>(), It.IsAny<CancellationToken>()))
            .Callback((AfterSalesAggregate a, CancellationToken _) => capturedSellerId = a.SellerId)
            .Returns(Task.CompletedTask);

        var uowMock = new Mock<IUnitOfWork>();
        var eventBusMock = new Mock<IEventBus>();
        var paymentMock = new Mock<IPaymentInfoQueryService>();
        var orderStatusProviderMock = new Mock<IOrderStatusProvider>();

        var svc = new AfterSalesAppService(
            repoMock.Object, eligibilityMock.Object, paymentMock.Object,
            orderStatusProviderMock.Object, eventBusMock.Object, uowMock.Object, NullLogger<AfterSalesAppService>.Instance);

        var dto = new SubmitAfterSalesDto
        {
            OrderId = orderId,
            SellerId = forgedSellerId,
            Type = AfterSalesType.RefundOnly,
            ReasonCategory = "quality",
            Reason = "broken item",
            RequestedAmount = 10m
        };

        var result = await svc.SubmitAfterSalesAsync(userId, dto);

        capturedSellerId.Should().NotBeNull();
        capturedSellerId.Should().Be(RealSellerId);
        capturedSellerId.Should().NotBe(forgedSellerId);
        result.SellerId.Should().Be(RealSellerId);
    }

    [Fact]
    public async Task SubmitAfterSalesAsync_Should_Propagate_Checker_Exception_And_Not_Save()
    {
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var eligibilityMock = new Mock<IAfterSalesEligibilityChecker>();
        eligibilityMock
            .Setup(c => c.EnsureEligibleAsync(orderId, It.IsAny<Guid?>(), userId, It.IsAny<AfterSalesType>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("售后期已过"));

        var repoMock = new Mock<IAfterSalesRepository>();
        var uowMock = new Mock<IUnitOfWork>();
        var eventBusMock = new Mock<IEventBus>();
        var paymentMock = new Mock<IPaymentInfoQueryService>();
        var orderStatusProviderMock = new Mock<IOrderStatusProvider>();

        var svc = new AfterSalesAppService(
            repoMock.Object, eligibilityMock.Object, paymentMock.Object,
            orderStatusProviderMock.Object, eventBusMock.Object, uowMock.Object, NullLogger<AfterSalesAppService>.Instance);

        var dto = new SubmitAfterSalesDto
        {
            OrderId = orderId,
            SellerId = Guid.NewGuid(),
            Type = AfterSalesType.RefundOnly,
            ReasonCategory = "quality",
            Reason = "broken item",
            RequestedAmount = 10m
        };

        var act = async () => await svc.SubmitAfterSalesAsync(userId, dto);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*售后期已过*");
        repoMock.Verify(r => r.AddAsync(It.IsAny<AfterSalesAggregate>(), It.IsAny<CancellationToken>()), Times.Never);
        uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
