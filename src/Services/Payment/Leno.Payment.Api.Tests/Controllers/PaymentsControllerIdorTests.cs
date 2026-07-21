using Leno.Infrastructure.Auth;
using Leno.Payment.Application;
using Leno.Payment.Application.DTOs;
using Leno.Payment.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Leno.Payment.Api.Tests.Controllers;

/// <summary>
/// P0-4 测试：验证 <see cref="PaymentsController"/> 买家端三个接口校验用户归属，防止 IDOR（不安全直接对象引用）。
/// 任意已认证 Buyer 不应能查询他人的支付单/退款单。
/// </summary>
public class PaymentsControllerIdorTests
{
    private static readonly Guid OwnerUserId = Guid.NewGuid();
    private static readonly Guid AttackerUserId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid PaymentId = Guid.NewGuid();
    private static readonly Guid AfterSalesId = Guid.NewGuid();

    /// <summary>
    /// 创建控制器，Mock 当前用户为 <paramref name="currentUserId"/>，
    /// 应用服务返回属于 <see cref="OwnerUserId"/> 的支付/退款数据。
    /// </summary>
    private static PaymentsController CreateController(Guid currentUserId)
    {
        var userContextMock = new Mock<ICurrentUserContext>();
        userContextMock.SetupGet(x => x.IsAuthenticated).Returns(true);
        userContextMock.SetupGet(x => x.UserId).Returns(currentUserId);

        var paymentAppMock = new Mock<IPaymentAppService>();
        var refundAppMock = new Mock<IRefundAppService>();

        // 模拟返回属于 OwnerUserId 的支付/退款数据
        paymentAppMock
            .Setup(s => s.GetPaymentResultAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentOrderDto
            {
                PaymentId = PaymentId,
                OrderId = OrderId,
                UserId = OwnerUserId,
                Amount = 100m,
                Channel = PaymentChannel.WeChatPay,
                Status = PaymentStatus.Pending
            });

        paymentAppMock
            .Setup(s => s.QueryPaymentStatusAsync(PaymentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelStatusDto
            {
                PaymentId = PaymentId,
                UserId = OwnerUserId,
                IsPaid = true,
                ChannelTradeNo = "CH001",
                PaidAt = DateTime.UtcNow
            });

        refundAppMock
            .Setup(s => s.GetRefundResultAsync(AfterSalesId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefundOrderDto
            {
                RefundId = Guid.NewGuid(),
                PaymentId = PaymentId,
                OrderId = OrderId,
                UserId = OwnerUserId,
                AfterSalesId = AfterSalesId,
                RefundAmount = 50m,
                Channel = PaymentChannel.WeChatPay,
                Status = RefundStatus.Succeeded
            });

        return new PaymentsController(userContextMock.Object, paymentAppMock.Object, refundAppMock.Object);
    }

    [Fact]
    public async Task GetPaymentResult_ShouldReturn403_When_UserDoesNotOwnOrder()
    {
        // Arrange：攻击者尝试查询他人的订单支付结果
        var sut = CreateController(AttackerUserId);

        // Act
        var result = await sut.GetPaymentResultAsync(OrderId, CancellationToken.None);

        // Assert：应返回 403 Forbidden，而非 200 OK
        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task GetPaymentResult_ShouldReturn200_When_UserOwnsOrder()
    {
        // Arrange：所有者查询自己的订单支付结果
        var sut = CreateController(OwnerUserId);

        // Act
        var result = await sut.GetPaymentResultAsync(OrderId, CancellationToken.None);

        // Assert：应返回 200 OK
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
    }

    [Fact]
    public async Task QueryPaymentStatus_ShouldReturn403_When_UserDoesNotOwnPayment()
    {
        // Arrange：攻击者尝试查询他人的支付状态
        var sut = CreateController(AttackerUserId);

        // Act
        var result = await sut.QueryPaymentStatusAsync(PaymentId, CancellationToken.None);

        // Assert：应返回 403
        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task QueryPaymentStatus_ShouldReturn200_When_UserOwnsPayment()
    {
        // Arrange：所有者查询自己的支付状态
        var sut = CreateController(OwnerUserId);

        // Act
        var result = await sut.QueryPaymentStatusAsync(PaymentId, CancellationToken.None);

        // Assert：应返回 200 OK
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
    }

    [Fact]
    public async Task GetRefundResult_ShouldReturn403_When_UserDoesNotOwnRefund()
    {
        // Arrange：攻击者尝试查询他人的退款结果
        var sut = CreateController(AttackerUserId);

        // Act
        var result = await sut.GetRefundResultAsync(AfterSalesId, CancellationToken.None);

        // Assert：应返回 403
        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task GetRefundResult_ShouldReturn200_When_UserOwnsRefund()
    {
        // Arrange：所有者查询自己的退款结果
        var sut = CreateController(OwnerUserId);

        // Act
        var result = await sut.GetRefundResultAsync(AfterSalesId, CancellationToken.None);

        // Assert：应返回 200 OK
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
    }

    [Fact]
    public async Task GetPaymentResult_ShouldReturn200_When_ResultIsNull()
    {
        // Arrange：支付单不存在时返回 200 + null data（不泄露是否存在）
        var userContextMock = new Mock<ICurrentUserContext>();
        userContextMock.SetupGet(x => x.IsAuthenticated).Returns(true);
        userContextMock.SetupGet(x => x.UserId).Returns(OwnerUserId);

        var paymentAppMock = new Mock<IPaymentAppService>();
        paymentAppMock
            .Setup(s => s.GetPaymentResultAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentOrderDto?)null);

        var refundAppMock = new Mock<IRefundAppService>();
        var sut = new PaymentsController(userContextMock.Object, paymentAppMock.Object, refundAppMock.Object);

        // Act
        var result = await sut.GetPaymentResultAsync(OrderId, CancellationToken.None);

        // Assert：null 结果不应触发归属校验，返回 200
        Assert.IsType<OkObjectResult>(result);
    }
}
