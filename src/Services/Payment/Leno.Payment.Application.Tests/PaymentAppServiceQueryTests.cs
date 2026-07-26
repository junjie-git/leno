using Leno.Payment.Application.DTOs;
using Leno.Payment.Application.Services;
using Leno.Payment.Domain.Aggregates;
using Leno.Payment.Domain.Repositories;
using Leno.Payment.Domain.Services;
using Leno.Payment.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Leno.Payment.Application.Tests;

/// <summary>
/// PaymentAppService.QueryPaymentsAsync 单元测试（BC8 第三梯队 P1 路径/能力对齐）。
/// 验证 QueryPaymentsAsync 将 paymentNo 与 orderId 透传到 IPaymentOrderRepository，
/// 同时验证分页与总数透传链路完整。
/// </summary>
public class PaymentAppServiceQueryTests
{
    private readonly Mock<IPaymentOrderRepository> _repoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IChannelStatusQueryService> _channelStatusMock = new();
    private readonly Mock<IPaymentOrderAntiCorruptionService> _orderAntiCorruptionMock = new();
    private readonly Mock<IPaymentChannelFactory> _channelFactoryMock = new();
    private readonly PaymentAppService _sut;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid PaymentId = Guid.NewGuid();

    public PaymentAppServiceQueryTests()
    {
        _sut = new PaymentAppService(
            _repoMock.Object,
            _uowMock.Object,
            _channelStatusMock.Object,
            _orderAntiCorruptionMock.Object,
            _channelFactoryMock.Object,
            NullLogger<PaymentAppService>.Instance);
    }

    /// <summary>
    /// 构造一笔 PaymentOrder 用于 Mock 返回数据。
    /// </summary>
    private static PaymentOrder CreatePayment()
    {
        return PaymentOrder.Create(PaymentId, OrderId, UserId, 100m, "CNY", PaymentChannel.WeChatPay);
    }

    /// <summary>
    /// 配置仓储 Mock，捕获 QueryAsync / CountAsync 的入参以便断言透传值。
    /// </summary>
    private void SetupRepoCapture(
        List<string?> capturedPaymentNos,
        List<Guid?> capturedOrderIds,
        List<int> capturedPages,
        List<int> capturedPageSizes)
    {
        _repoMock
            .Setup(r => r.QueryAsync(
                It.IsAny<Guid?>(),
                It.IsAny<PaymentChannel?>(),
                It.IsAny<PaymentStatus?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<Guid?>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Callback<Guid?, PaymentChannel?, PaymentStatus?, DateTime?, DateTime?, string?, Guid?, int, int, CancellationToken>(
                (_, _, _, _, _, paymentNo, orderId, page, pageSize, _) =>
                {
                    capturedPaymentNos.Add(paymentNo);
                    capturedOrderIds.Add(orderId);
                    capturedPages.Add(page);
                    capturedPageSizes.Add(pageSize);
                })
            .ReturnsAsync(new List<PaymentOrder> { CreatePayment() });

        _repoMock
            .Setup(r => r.CountAsync(
                It.IsAny<Guid?>(),
                It.IsAny<PaymentChannel?>(),
                It.IsAny<PaymentStatus?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .Callback<Guid?, PaymentChannel?, PaymentStatus?, DateTime?, DateTime?, string?, Guid?, CancellationToken>(
                (_, _, _, _, _, paymentNo, orderId, _) =>
                {
                    capturedPaymentNos.Add(paymentNo);
                    capturedOrderIds.Add(orderId);
                })
            .ReturnsAsync(1);
    }

    [Fact]
    public async Task QueryPaymentsAsync_WithPaymentNoAndOrderId_ShouldPassThroughToRepository()
    {
        // 安排：传入 paymentNo="PAY2026" 与 orderId，验证透传到仓储的 QueryAsync 与 CountAsync
        var capturedPaymentNos = new List<string?>();
        var capturedOrderIds = new List<Guid?>();
        var capturedPages = new List<int>();
        var capturedPageSizes = new List<int>();
        SetupRepoCapture(capturedPaymentNos, capturedOrderIds, capturedPages, capturedPageSizes);

        // 行动
        var result = await _sut.QueryPaymentsAsync(
            userId: null,
            channel: null,
            status: null,
            startDate: null,
            endDate: null,
            paymentNo: "PAY2026",
            orderId: OrderId,
            page: 2,
            pageSize: 15);

        // 断言：返回值正确
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        result.Total.Should().Be(1);
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(15);

        // 断言：QueryAsync 与 CountAsync 均被调用一次，paymentNo 与 orderId 正确透传
        capturedPaymentNos.Should().HaveCount(2);
        Assert.All(capturedPaymentNos, pn => Assert.Equal("PAY2026", pn));
        capturedOrderIds.Should().HaveCount(2);
        Assert.All(capturedOrderIds, oid => Assert.Equal(OrderId, oid));

        // 分页参数（仅 QueryAsync 捕获）正确透传
        capturedPages.Should().ContainSingle().Which.Should().Be(2);
        capturedPageSizes.Should().ContainSingle().Which.Should().Be(15);

        _repoMock.Verify(r => r.QueryAsync(
            null, null, null, null, null, "PAY2026", OrderId, 2, 15,
            It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(r => r.CountAsync(
            null, null, null, null, null, "PAY2026", OrderId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueryPaymentsAsync_WithNullPaymentNoAndOrderId_ShouldPassNullToRepository()
    {
        // 安排：未传 paymentNo / orderId（null），验证 null 透传给仓储
        var capturedPaymentNos = new List<string?>();
        var capturedOrderIds = new List<Guid?>();
        var capturedPages = new List<int>();
        var capturedPageSizes = new List<int>();
        SetupRepoCapture(capturedPaymentNos, capturedOrderIds, capturedPages, capturedPageSizes);

        // 行动
        var result = await _sut.QueryPaymentsAsync(
            userId: null,
            channel: null,
            status: null,
            startDate: null,
            endDate: null,
            paymentNo: null,
            orderId: null,
            page: 1,
            pageSize: 20);

        // 断言：null 正确透传
        result.Should().NotBeNull();
        capturedPaymentNos.Should().HaveCount(2);
        Assert.All(capturedPaymentNos, pn => Assert.Null(pn));
        capturedOrderIds.Should().HaveCount(2);
        Assert.All(capturedOrderIds, oid => Assert.Null(oid));

        _repoMock.Verify(r => r.QueryAsync(
            null, null, null, null, null, null, null, 1, 20,
            It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(r => r.CountAsync(
            null, null, null, null, null, null, null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueryPaymentsAsync_WithEmptyPaymentNo_ShouldPassEmptyToRepository()
    {
        // 安排：传入空字符串 paymentNo，验证应用层不做归一化，直接透传给仓储
        // （仓储实现使用 string.IsNullOrWhiteSpace 判定，空字符串与 null 行为一致不过滤）
        var capturedPaymentNos = new List<string?>();
        var capturedOrderIds = new List<Guid?>();
        var capturedPages = new List<int>();
        var capturedPageSizes = new List<int>();
        SetupRepoCapture(capturedPaymentNos, capturedOrderIds, capturedPages, capturedPageSizes);

        // 行动
        await _sut.QueryPaymentsAsync(
            userId: null,
            channel: null,
            status: null,
            startDate: null,
            endDate: null,
            paymentNo: string.Empty,
            orderId: null,
            page: 1,
            pageSize: 10);

        // 断言：空字符串透传（仓储侧由 IsNullOrWhiteSpace 处理）
        capturedPaymentNos.Should().HaveCount(2);
        Assert.All(capturedPaymentNos, pn => Assert.Equal(string.Empty, pn));
        _repoMock.Verify(r => r.QueryAsync(
            null, null, null, null, null, string.Empty, null, 1, 10,
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
