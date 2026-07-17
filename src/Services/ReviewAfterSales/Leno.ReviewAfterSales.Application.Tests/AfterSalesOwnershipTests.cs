using Leno.Infrastructure.Abstractions;
using Leno.ReviewAfterSales.Application.Services;
using Leno.ReviewAfterSales.Domain.Aggregates;
using Leno.ReviewAfterSales.Domain.Exceptions;
using Leno.ReviewAfterSales.Domain.Repositories;
using Leno.ReviewAfterSales.Domain.Services;
using Leno.ReviewAfterSales.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using Moq;
using AfterSalesAggregate = Leno.ReviewAfterSales.Domain.Aggregates.AfterSales;

namespace Leno.ReviewAfterSales.Application.Tests;

/// <summary>
/// AfterSalesAppService 审核/确认退货卖家归属越权校验单元测试。
/// 验证非归属卖家审核/确认退货被拒（抛 AFTERSALES_NOT_OWNED），归属卖家审核成功。
/// </summary>
public class AfterSalesOwnershipTests
{
    private readonly Mock<IAfterSalesRepository> _afterSalesRepoMock = new();
    private readonly Mock<IAfterSalesEligibilityChecker> _eligibilityMock = new();
    private readonly Mock<IPaymentInfoQueryService> _paymentInfoMock = new();
    private readonly Mock<IEventBus> _eventBusMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<ILogger<AfterSalesAppService>> _loggerMock = new();
    private readonly AfterSalesAppService _sut;

    private static readonly Guid AfterSalesId = Guid.NewGuid();
    private static readonly Guid OwnerSellerId = Guid.NewGuid();
    private static readonly Guid OtherSellerId = Guid.NewGuid();

    public AfterSalesOwnershipTests()
    {
        _sut = new AfterSalesAppService(
            _afterSalesRepoMock.Object, _eligibilityMock.Object, _paymentInfoMock.Object,
            _eventBusMock.Object, _uowMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task ApproveAfterSalesAsync_NonOwnerSeller_ShouldThrow403()
    {
        // Arrange: 售后单归属 OwnerSellerId，调用方是 OtherSellerId
        var afterSales = CreatePendingAfterSales(OwnerSellerId);
        _afterSalesRepoMock.Setup(r => r.GetByIdAsync(AfterSalesId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(afterSales);

        // Act & Assert
        var act = () => _sut.ApproveAfterSalesAsync(AfterSalesId, OtherSellerId, 100m, CancellationToken.None);
        await act.Should().ThrowAsync<ReviewDomainException>()
            .Where(ex => ex.ErrorCode == "AFTERSALES_NOT_OWNED");

        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConfirmReturnAsync_NonOwnerSeller_ShouldThrow403()
    {
        // Arrange: 售后单已推进到 ReturnGoods 态，归属 OwnerSellerId，调用方是 OtherSellerId
        var afterSales = CreateReturnReceivedAfterSales(OwnerSellerId);
        _afterSalesRepoMock.Setup(r => r.GetByIdAsync(AfterSalesId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(afterSales);

        // Act & Assert
        var act = () => _sut.ConfirmReturnAsync(AfterSalesId, OtherSellerId, CancellationToken.None);
        await act.Should().ThrowAsync<ReviewDomainException>()
            .Where(ex => ex.ErrorCode == "AFTERSALES_NOT_OWNED");

        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ApproveAfterSalesAsync_OwnerSeller_ShouldSucceed()
    {
        // Arrange: 归属卖家调用（仅退款类型，审核通过后进入退款流程）
        var afterSales = CreatePendingAfterSales(OwnerSellerId);
        _afterSalesRepoMock.Setup(r => r.GetByIdAsync(AfterSalesId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(afterSales);
        _paymentInfoMock.Setup(p => p.GetByOrderIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentInfoResult { PaymentId = Guid.NewGuid(), Channel = "WeChatPay" });

        // Act
        await _sut.ApproveAfterSalesAsync(AfterSalesId, OwnerSellerId, 100m, CancellationToken.None);

        // Assert
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static AfterSalesAggregate CreatePendingAfterSales(Guid sellerId) =>
        AfterSalesAggregate.Create(
            AfterSalesId, Guid.NewGuid(), null, Guid.NewGuid(), sellerId,
            AfterSalesType.RefundOnly, "质量问题", "商品损坏", new List<string>(),
            100m, "CNY");

    private static AfterSalesAggregate CreateReturnReceivedAfterSales(Guid sellerId)
    {
        // 推进售后状态机到 ReturnGoods（买家已退货，待卖家确认收货）：
        // Pending → Approve → ReturnGoods
        var afterSales = AfterSalesAggregate.Create(
            AfterSalesId, Guid.NewGuid(), null, Guid.NewGuid(), sellerId,
            AfterSalesType.ReturnRefund, "质量问题", "商品损坏", new List<string>(),
            100m, "CNY");
        // 审核通过（用归属卖家作为审核人，仅用于推进状态机）
        afterSales.Approve(sellerId, 100m);
        // 买家已退货
        afterSales.ReturnGoods("TRACKING-001");
        return afterSales;
    }
}
