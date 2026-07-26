using Leno.Payment.Application;
using Leno.Payment.Application.DTOs;
using Leno.Payment.Application.Services;
using Leno.Payment.Domain.Aggregates;
using Leno.Payment.Domain.Repositories;
using Leno.Payment.Domain.Services;
using Leno.Payment.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using Moq;

namespace Leno.Payment.Application.Tests;

public class PaymentAppServiceTests
{
    private readonly Mock<IPaymentOrderRepository> _repoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IChannelStatusQueryService> _channelStatusMock = new();
    private readonly Mock<IPaymentOrderAntiCorruptionService> _orderAntiCorruptionMock = new();
    private readonly Mock<IPaymentChannelFactory> _channelFactoryMock = new();
    private readonly Mock<ILogger<PaymentAppService>> _loggerMock = new();
    private readonly PaymentAppService _sut;

    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid PaymentId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    public PaymentAppServiceTests()
    {
        _sut = new PaymentAppService(
            _repoMock.Object,
            _uowMock.Object,
            _channelStatusMock.Object,
            _orderAntiCorruptionMock.Object,
            _channelFactoryMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task GetPaymentResultAsync_Existing_ShouldReturnDto()
    {
        var payment = CreatePayment();
        _repoMock.Setup(r => r.GetByOrderIdAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(payment);

        var result = await _sut.GetPaymentResultAsync(OrderId);

        result.Should().NotBeNull();
        result!.OrderId.Should().Be(OrderId);
        result.Amount.Should().Be(100m);
    }

    [Fact]
    public async Task GetPaymentResultAsync_NotFound_ShouldReturnNull()
    {
        _repoMock.Setup(r => r.GetByOrderIdAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync((PaymentOrder?)null);

        var result = await _sut.GetPaymentResultAsync(OrderId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task QueryPaymentStatusAsync_AlreadyPaid_ShouldReturnWithoutQuerying()
    {
        var payment = CreatePayment();
        payment.MarkSucceeded("TRADE001", 100m, DateTime.UtcNow);
        _repoMock.Setup(r => r.GetByIdAsync(PaymentId, It.IsAny<CancellationToken>())).ReturnsAsync(payment);

        var result = await _sut.QueryPaymentStatusAsync(PaymentId);

        result.IsPaid.Should().BeTrue();
        _channelStatusMock.Verify(s => s.QueryPaymentStatusAsync(It.IsAny<PaymentChannel>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task QueryPaymentStatusAsync_ChannelPaid_ShouldCompensate()
    {
        var payment = CreatePayment();
        _repoMock.Setup(r => r.GetByIdAsync(PaymentId, It.IsAny<CancellationToken>())).ReturnsAsync(payment);
        _channelStatusMock.Setup(s => s.QueryPaymentStatusAsync(PaymentChannel.WeChatPay, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelStatusResult { IsPaid = true, ChannelTradeNo = "CH001", PaidAt = DateTime.UtcNow, Amount = 100m });

        var result = await _sut.QueryPaymentStatusAsync(PaymentId);

        result.IsPaid.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Paid);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueryPaymentStatusAsync_ChannelPaidWithMismatchedAmount_ShouldNotMarkSucceeded()
    {
        // 渠道返回已支付但金额不一致（攻击者构造的低金额支付），不应标记成功，进入人工对账
        var payment = CreatePayment();
        _repoMock.Setup(r => r.GetByIdAsync(PaymentId, It.IsAny<CancellationToken>())).ReturnsAsync(payment);
        _channelStatusMock.Setup(s => s.QueryPaymentStatusAsync(PaymentChannel.WeChatPay, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelStatusResult { IsPaid = true, ChannelTradeNo = "CH001", PaidAt = DateTime.UtcNow, Amount = 0.01m });

        var result = await _sut.QueryPaymentStatusAsync(PaymentId);

        result.IsPaid.Should().BeFalse();
        payment.Status.Should().Be(PaymentStatus.Pending);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task QueryPaymentStatusAsync_ChannelPaidWithoutAmount_ShouldNotMarkSucceeded()
    {
        // 渠道返回已支付但未携带金额信息，无法强校验，进入人工对账
        var payment = CreatePayment();
        _repoMock.Setup(r => r.GetByIdAsync(PaymentId, It.IsAny<CancellationToken>())).ReturnsAsync(payment);
        _channelStatusMock.Setup(s => s.QueryPaymentStatusAsync(PaymentChannel.WeChatPay, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelStatusResult { IsPaid = true, ChannelTradeNo = "CH001", PaidAt = DateTime.UtcNow, Amount = null });

        var result = await _sut.QueryPaymentStatusAsync(PaymentId);

        result.IsPaid.Should().BeFalse();
        payment.Status.Should().Be(PaymentStatus.Pending);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task QueryPaymentsAsync_ShouldReturnPaginated()
    {
        var payments = new List<PaymentOrder> { CreatePayment() };
        _repoMock.Setup(r => r.QueryAsync(null, null, null, null, null, null, null, 1, 20, It.IsAny<CancellationToken>())).ReturnsAsync(payments);
        _repoMock.Setup(r => r.CountAsync(null, null, null, null, null, null, null, It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _sut.QueryPaymentsAsync(null, null, null, null, null, null, null, 1, 20);

        result.Items.Should().HaveCount(1);
        result.Total.Should().Be(1);
    }

    private static PaymentOrder CreatePayment()
    {
        return PaymentOrder.Create(PaymentId, OrderId, UserId, 100m, "CNY", PaymentChannel.WeChatPay);
    }
}

public class RefundAppServiceTests
{
    private readonly Mock<IRefundOrderRepository> _repoMock = new();
    private readonly RefundAppService _sut;

    private static readonly Guid AfterSalesId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();

    public RefundAppServiceTests()
    {
        _sut = new RefundAppService(_repoMock.Object);
    }

    [Fact]
    public async Task GetRefundResultAsync_Existing_ShouldReturnDto()
    {
        var refund = CreateRefund();
        _repoMock.Setup(r => r.GetByAfterSalesIdAsync(AfterSalesId, It.IsAny<CancellationToken>())).ReturnsAsync(refund);

        var result = await _sut.GetRefundResultAsync(AfterSalesId);

        result.Should().NotBeNull();
        result!.AfterSalesId.Should().Be(AfterSalesId);
        result.RefundAmount.Should().Be(50m);
    }

    [Fact]
    public async Task GetRefundResultAsync_NotFound_ShouldReturnNull()
    {
        _repoMock.Setup(r => r.GetByAfterSalesIdAsync(AfterSalesId, It.IsAny<CancellationToken>())).ReturnsAsync((RefundOrder?)null);

        var result = await _sut.GetRefundResultAsync(AfterSalesId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task QueryRefundsAsync_ShouldReturnPaginated()
    {
        var refunds = new List<RefundOrder> { CreateRefund() };
        _repoMock.Setup(r => r.QueryAsync(null, null, null, null, null, 1, 20, It.IsAny<CancellationToken>())).ReturnsAsync(refunds);
        _repoMock.Setup(r => r.CountAsync(null, null, null, null, null, It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _sut.QueryRefundsAsync(null, null, null, null, null, 1, 20);

        result.Items.Should().HaveCount(1);
        result.Total.Should().Be(1);
    }

    private static RefundOrder CreateRefund()
    {
        return RefundOrder.Create(
            Guid.NewGuid(), Guid.NewGuid(), OrderId, Guid.NewGuid(), AfterSalesId,
            50m, "CNY", "PAY20260701000001", PaymentChannel.WeChatPay);
    }
}