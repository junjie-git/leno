using Leno.ReviewAfterSales.Domain.Aggregates;
using Leno.ReviewAfterSales.Domain.Exceptions;
using Leno.ReviewAfterSales.Domain.ValueObjects;
using Xunit;

namespace Leno.ReviewAfterSales.UnitTests.Domain;

/// <summary>
/// 审计 3.2：AfterSales.ConfirmReturn 未记录操作人。
/// 验证 ConfirmReturn(operatorId) 后 ReturnConfirmedBy == operatorId；operatorId == Guid.Empty 抛领域异常。
/// </summary>
public sealed class AfterSalesConfirmReturnOperatorTests
{
    private static AfterSales CreateReturnGoodsState()
    {
        var sellerId = Guid.NewGuid();
        var afterSales = AfterSales.Create(
            Guid.NewGuid(), Guid.NewGuid(), null,
            userId: Guid.NewGuid(), sellerId: sellerId,
            AfterSalesType.ReturnRefund, "quality", "broken", null, 10m, "CNY");
        afterSales.Approve(sellerId, 10m);
        afterSales.ReturnGoods("TRACK001");
        return afterSales;
    }

    [Fact]
    public void ConfirmReturn_Should_Record_ReturnConfirmedBy_When_OperatorId_Valid()
    {
        var afterSales = CreateReturnGoodsState();
        var sellerId = afterSales.SellerId;

        afterSales.ConfirmReturn(sellerId);

        Assert.Equal(AfterSalesStatus.ConfirmReturn, afterSales.Status);
        Assert.Equal(sellerId, afterSales.ReturnConfirmedBy);
    }

    [Fact]
    public void ConfirmReturn_Should_Throw_When_OperatorId_Empty()
    {
        var afterSales = CreateReturnGoodsState();

        var ex = Assert.Throws<ReviewDomainException>(() => afterSales.ConfirmReturn(Guid.Empty));
        Assert.Equal("AFTERSALES_OPERATOR_EMPTY", ex.ErrorCode);
    }
}
