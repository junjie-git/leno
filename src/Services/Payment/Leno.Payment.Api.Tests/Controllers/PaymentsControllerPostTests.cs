using Leno.Infrastructure.Auth;
using Leno.Payment.Api.Controllers;
using Leno.Payment.Application;
using Leno.Payment.Application.DTOs;
using Leno.Payment.Domain.ValueObjects;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Leno.Payment.Api.Tests.Controllers;

/// <summary>
/// POST /api/payments 发起支付端点的控制器层单元测试（spec F-PAY-001）。
/// 验证 <see cref="PaymentsController.PostAsync"/> 的鉴权用户解析、请求体校验、
/// 服务调用与统一响应封装。
/// </summary>
public class PaymentsControllerPostTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();

    /// <summary>
    /// 创建控制器，Mock 当前用户为 <see cref="UserId"/>，应用服务按 <paramref name="serviceResult"/> 返回。
    /// </summary>
    /// <param name="serviceResult">应用服务返回的发起支付结果，默认为成功 ChannelOrdered 结果。</param>
    /// <returns>控制器实例与 PaymentAppService Mock，供测试进一步断言。</returns>
    private static (PaymentsController controller, Mock<IPaymentAppService> paymentAppMock) CreateController(
        PaymentInitiationResultDto? serviceResult = null)
    {
        var userContextMock = new Mock<ICurrentUserContext>();
        userContextMock.SetupGet(x => x.IsAuthenticated).Returns(true);
        userContextMock.SetupGet(x => x.UserId).Returns(UserId);

        serviceResult ??= new PaymentInitiationResultDto
        {
            PaymentOrderId = Guid.NewGuid(),
            PaymentNo = "PAY20260726000001",
            OrderId = OrderId,
            Channel = PaymentChannel.WeChatPay,
            Status = PaymentStatus.ChannelOrdered,
            PrepayId = "wx_prepay_001",
            CodeUrl = "weixin://wxpay/bizpayurl?pr=test",
            H5Url = null,
            ExpireAt = DateTime.UtcNow.AddHours(2),
            FailReason = null
        };

        var paymentAppMock = new Mock<IPaymentAppService>();
        paymentAppMock.Setup(s => s.CreatePaymentAsync(UserId, It.IsAny<CreatePaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceResult);

        var refundAppMock = new Mock<IRefundAppService>();
        var controller = new PaymentsController(userContextMock.Object, paymentAppMock.Object, refundAppMock.Object);
        return (controller, paymentAppMock);
    }

    [Fact]
    public async Task PostAsync_WithValidRequest_ShouldReturnOkWithInitiationResult()
    {
        // 安排
        var (sut, _) = CreateController();
        var request = new CreatePaymentRequest
        {
            OrderId = OrderId,
            Channel = PaymentChannel.WeChatPay,
            Scene = TradeType.Native
        };

        // 行动
        var result = await sut.PostAsync(request, CancellationToken.None);

        // 断言：返回 200 OK，封装 ApiResponse<PaymentInitiationResultDto>
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);

        var response = Assert.IsType<ApiResponse<PaymentInitiationResultDto>>(okResult.Value);
        Assert.Equal(200, response.Code);
        Assert.NotNull(response.Data);
        response.Data!.OrderId.Should().Be(OrderId);
        response.Data.Channel.Should().Be(PaymentChannel.WeChatPay);
        response.Data.Status.Should().Be(PaymentStatus.ChannelOrdered);
        response.Data.PrepayId.Should().Be("wx_prepay_001");
        response.Data.CodeUrl.Should().Be("weixin://wxpay/bizpayurl?pr=test");
    }

    [Fact]
    public async Task PostAsync_WithNullRequest_ShouldThrowArgumentNullException()
    {
        // 安排
        var (sut, _) = CreateController();

        // 行动 + 断言：null 请求体应抛 ArgumentNullException（由全局异常中间件映射为 400）
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.PostAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task PostAsync_ShouldPassCurrentUserIdToService()
    {
        // 安排：验证控制器从 ICurrentUserContext 解析的当前用户标识传入应用服务
        var (sut, paymentAppMock) = CreateController();
        var request = new CreatePaymentRequest
        {
            OrderId = OrderId,
            Channel = PaymentChannel.WeChatPay
        };

        // 行动
        await sut.PostAsync(request, CancellationToken.None);

        // 断言：应用服务应收到从 JWT 解析的当前用户标识（而非请求体传入）
        paymentAppMock.Verify(
            s => s.CreatePaymentAsync(UserId, It.IsAny<CreatePaymentRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PostAsync_ShouldPassRequestToService()
    {
        // 安排：验证请求体的 OrderId、Channel、Scene 字段透传给应用服务
        var (sut, paymentAppMock) = CreateController();
        var request = new CreatePaymentRequest
        {
            OrderId = OrderId,
            Channel = PaymentChannel.Alipay,
            Scene = TradeType.H5,
            IdempotencyKey = "idem-key-001"
        };

        // 行动
        await sut.PostAsync(request, CancellationToken.None);

        // 断言：捕获传给应用服务的请求参数，校验字段透传
        CreatePaymentRequest? capturedRequest = null;
        paymentAppMock.Verify(
            s => s.CreatePaymentAsync(
                UserId,
                It.Is<CreatePaymentRequest>(r => CaptureRequest(r, out capturedRequest)),
                It.IsAny<CancellationToken>()),
            Times.Once);
        capturedRequest.Should().NotBeNull();
        capturedRequest!.OrderId.Should().Be(OrderId);
        capturedRequest.Channel.Should().Be(PaymentChannel.Alipay);
        capturedRequest.Scene.Should().Be(TradeType.H5);
        capturedRequest.IdempotencyKey.Should().Be("idem-key-001");
    }

    [Fact]
    public async Task PostAsync_WhenUserNotAuthenticated_ShouldThrowUnauthorizedAccessException()
    {
        // 安排：未认证用户调用发起支付端点，应抛 UnauthorizedAccessException（由全局异常中间件映射为 401）
        var userContextMock = new Mock<ICurrentUserContext>();
        userContextMock.SetupGet(x => x.IsAuthenticated).Returns(false);
        userContextMock.SetupGet(x => x.UserId).Returns((Guid?)null);

        var paymentAppMock = new Mock<IPaymentAppService>();
        var refundAppMock = new Mock<IRefundAppService>();
        var sut = new PaymentsController(userContextMock.Object, paymentAppMock.Object, refundAppMock.Object);

        var request = new CreatePaymentRequest { OrderId = OrderId, Channel = PaymentChannel.WeChatPay };

        // 行动 + 断言
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sut.PostAsync(request, CancellationToken.None));
        paymentAppMock.Verify(
            s => s.CreatePaymentAsync(It.IsAny<Guid>(), It.IsAny<CreatePaymentRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PostAsync_ShouldPassCancellationTokenToService()
    {
        // 安排：验证取消令牌透传给应用服务，支持客户端取消长耗时渠道调用
        var (sut, paymentAppMock) = CreateController();
        var request = new CreatePaymentRequest { OrderId = OrderId, Channel = PaymentChannel.WeChatPay };
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        // 行动
        await sut.PostAsync(request, ct);

        // 断言：取消令牌应透传给应用服务
        paymentAppMock.Verify(
            s => s.CreatePaymentAsync(UserId, It.IsAny<CreatePaymentRequest>(), ct),
            Times.Once);
    }

    /// <summary>辅助方法：捕获 <see cref="CreatePaymentRequest"/> 参数到 <paramref name="captured"/>。</summary>
    private static bool CaptureRequest(CreatePaymentRequest request, out CreatePaymentRequest? captured)
    {
        captured = request;
        return true;
    }
}
