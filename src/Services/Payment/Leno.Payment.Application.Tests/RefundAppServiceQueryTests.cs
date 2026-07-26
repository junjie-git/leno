using Leno.Payment.Application.Services;
using Leno.Payment.Domain.Aggregates;
using Leno.Payment.Domain.Repositories;
using Leno.Payment.Domain.ValueObjects;
using Moq;
using Xunit;

namespace Leno.Payment.Application.Tests;

/// <summary>
/// RefundAppService.QueryRefundsAsync 单元测试（BC8 第三梯队 P1 路径/能力对齐）。
/// 验证 QueryRefundsAsync 将 refundNo、startDate、endDate 透传到 IRefundOrderRepository，
/// 同时验证分页与总数透传链路完整。
/// </summary>
public class RefundAppServiceQueryTests
{
    private readonly Mock<IRefundOrderRepository> _repoMock = new();
    private readonly RefundAppService _sut;

    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid AfterSalesId = Guid.NewGuid();
    private static readonly Guid RefundId = Guid.NewGuid();

    public RefundAppServiceQueryTests()
    {
        _sut = new RefundAppService(_repoMock.Object);
    }

    /// <summary>
    /// 构造一笔 RefundOrder 用于 Mock 返回数据。
    /// </summary>
    private static RefundOrder CreateRefund()
    {
        return RefundOrder.Create(
            RefundId, Guid.NewGuid(), OrderId, Guid.NewGuid(), AfterSalesId,
            50m, "CNY", "PAY20260701000001", PaymentChannel.WeChatPay);
    }

    /// <summary>
    /// 配置仓储 Mock，捕获 QueryAsync / CountAsync 的入参以便断言透传值。
    /// </summary>
    private void SetupRepoCapture(
        List<string?> capturedRefundNos,
        List<DateTime?> capturedStartDates,
        List<DateTime?> capturedEndDates,
        List<int> capturedPages,
        List<int> capturedPageSizes)
    {
        _repoMock
            .Setup(r => r.QueryAsync(
                It.IsAny<Guid?>(),
                It.IsAny<RefundStatus?>(),
                It.IsAny<string?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Callback<Guid?, RefundStatus?, string?, DateTime?, DateTime?, int, int, CancellationToken>(
                (_, _, refundNo, startDate, endDate, page, pageSize, _) =>
                {
                    capturedRefundNos.Add(refundNo);
                    capturedStartDates.Add(startDate);
                    capturedEndDates.Add(endDate);
                    capturedPages.Add(page);
                    capturedPageSizes.Add(pageSize);
                })
            .ReturnsAsync(new List<RefundOrder> { CreateRefund() });

        _repoMock
            .Setup(r => r.CountAsync(
                It.IsAny<Guid?>(),
                It.IsAny<RefundStatus?>(),
                It.IsAny<string?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Callback<Guid?, RefundStatus?, string?, DateTime?, DateTime?, CancellationToken>(
                (_, _, refundNo, startDate, endDate, _) =>
                {
                    capturedRefundNos.Add(refundNo);
                    capturedStartDates.Add(startDate);
                    capturedEndDates.Add(endDate);
                })
            .ReturnsAsync(1);
    }

    [Fact]
    public async Task QueryRefundsAsync_WithRefundNoAndDateRange_ShouldPassThroughToRepository()
    {
        // 安排：传入 refundNo="RFD2026" 与 startDate/endDate，验证透传到仓储的 QueryAsync 与 CountAsync
        var capturedRefundNos = new List<string?>();
        var capturedStartDates = new List<DateTime?>();
        var capturedEndDates = new List<DateTime?>();
        var capturedPages = new List<int>();
        var capturedPageSizes = new List<int>();
        SetupRepoCapture(capturedRefundNos, capturedStartDates, capturedEndDates, capturedPages, capturedPageSizes);

        var startDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2026, 7, 31, 23, 59, 59, DateTimeKind.Utc);

        // 行动
        var result = await _sut.QueryRefundsAsync(
            orderId: null,
            status: null,
            refundNo: "RFD2026",
            startDate: startDate,
            endDate: endDate,
            page: 2,
            pageSize: 15);

        // 断言：返回值正确
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        result.Total.Should().Be(1);
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(15);

        // 断言：QueryAsync 与 CountAsync 均被调用一次，refundNo 与日期范围正确透传
        capturedRefundNos.Should().HaveCount(2);
        Assert.All(capturedRefundNos, rn => Assert.Equal("RFD2026", rn));
        capturedStartDates.Should().HaveCount(2);
        Assert.All(capturedStartDates, sd => Assert.Equal(startDate, sd));
        capturedEndDates.Should().HaveCount(2);
        Assert.All(capturedEndDates, ed => Assert.Equal(endDate, ed));

        // 分页参数（仅 QueryAsync 捕获）正确透传
        capturedPages.Should().ContainSingle().Which.Should().Be(2);
        capturedPageSizes.Should().ContainSingle().Which.Should().Be(15);

        _repoMock.Verify(r => r.QueryAsync(
            null, null, "RFD2026", startDate, endDate, 2, 15,
            It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(r => r.CountAsync(
            null, null, "RFD2026", startDate, endDate,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueryRefundsAsync_WithNullFilters_ShouldPassNullToRepository()
    {
        // 安排：未传 refundNo / startDate / endDate（全 null），验证 null 透传给仓储
        var capturedRefundNos = new List<string?>();
        var capturedStartDates = new List<DateTime?>();
        var capturedEndDates = new List<DateTime?>();
        var capturedPages = new List<int>();
        var capturedPageSizes = new List<int>();
        SetupRepoCapture(capturedRefundNos, capturedStartDates, capturedEndDates, capturedPages, capturedPageSizes);

        // 行动
        var result = await _sut.QueryRefundsAsync(
            orderId: null,
            status: null,
            refundNo: null,
            startDate: null,
            endDate: null,
            page: 1,
            pageSize: 20);

        // 断言：null 正确透传
        result.Should().NotBeNull();
        capturedRefundNos.Should().HaveCount(2);
        Assert.All(capturedRefundNos, rn => Assert.Null(rn));
        capturedStartDates.Should().HaveCount(2);
        Assert.All(capturedStartDates, sd => Assert.Null(sd));
        capturedEndDates.Should().HaveCount(2);
        Assert.All(capturedEndDates, ed => Assert.Null(ed));

        _repoMock.Verify(r => r.QueryAsync(
            null, null, null, null, null, 1, 20,
            It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(r => r.CountAsync(
            null, null, null, null, null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueryRefundsAsync_WithEmptyRefundNo_ShouldPassEmptyToRepository()
    {
        // 安排：传入空字符串 refundNo，验证应用层不做归一化，直接透传给仓储
        // （仓储实现使用 string.IsNullOrWhiteSpace 判定，空字符串与 null 行为一致不过滤）
        var capturedRefundNos = new List<string?>();
        var capturedStartDates = new List<DateTime?>();
        var capturedEndDates = new List<DateTime?>();
        var capturedPages = new List<int>();
        var capturedPageSizes = new List<int>();
        SetupRepoCapture(capturedRefundNos, capturedStartDates, capturedEndDates, capturedPages, capturedPageSizes);

        // 行动
        await _sut.QueryRefundsAsync(
            orderId: null,
            status: null,
            refundNo: string.Empty,
            startDate: null,
            endDate: null,
            page: 1,
            pageSize: 10);

        // 断言：空字符串透传（仓储侧由 IsNullOrWhiteSpace 处理）
        capturedRefundNos.Should().HaveCount(2);
        Assert.All(capturedRefundNos, rn => Assert.Equal(string.Empty, rn));
        _repoMock.Verify(r => r.QueryAsync(
            null, null, string.Empty, null, null, 1, 10,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueryRefundsAsync_WithOnlyStartDate_ShouldPassSingleDateToRepository()
    {
        // 安排：仅传 startDate 不传 endDate，验证部分日期范围透传
        var capturedRefundNos = new List<string?>();
        var capturedStartDates = new List<DateTime?>();
        var capturedEndDates = new List<DateTime?>();
        var capturedPages = new List<int>();
        var capturedPageSizes = new List<int>();
        SetupRepoCapture(capturedRefundNos, capturedStartDates, capturedEndDates, capturedPages, capturedPageSizes);

        var startDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // 行动
        await _sut.QueryRefundsAsync(
            orderId: null,
            status: null,
            refundNo: null,
            startDate: startDate,
            endDate: null,
            page: 1,
            pageSize: 50);

        // 断言：startDate 透传、endDate 为 null
        capturedStartDates.Should().HaveCount(2);
        Assert.All(capturedStartDates, sd => Assert.Equal(startDate, sd));
        capturedEndDates.Should().HaveCount(2);
        Assert.All(capturedEndDates, ed => Assert.Null(ed));

        _repoMock.Verify(r => r.QueryAsync(
            null, null, null, startDate, null, 1, 50,
            It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(r => r.CountAsync(
            null, null, null, startDate, null,
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
