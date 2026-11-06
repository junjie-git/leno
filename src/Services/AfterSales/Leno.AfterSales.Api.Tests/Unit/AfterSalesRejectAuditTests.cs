using Leno.AfterSales.Domain.Aggregates;
using Leno.AfterSales.Domain.ValueObjects;
using Xunit;

namespace Leno.AfterSales.Api.Tests.Unit;

/// <summary>
/// 审计 3.1：售后聚合 Reject 误用 ApprovedAt 字段记录驳回时间。
/// 验证驳回后 ApprovedAt == null 且 RejectedAt.HasValue；审核同意后 ApprovedAt.HasValue 且 RejectedAt == null。
/// </summary>
public sealed class AfterSalesRejectAuditTests
{
    private static AfterSalesOrder CreatePending()
        => AfterSalesOrder.Create(
            Guid.NewGuid(), Guid.NewGuid(), null,
            userId: Guid.NewGuid(), sellerId: Guid.NewGuid(),
            AfterSalesType.RefundOnly, "quality", "broken", new List<string>(), 10m, "CNY");

    [Fact]
    public void Reject_Should_Set_RejectedAt_And_Keep_ApprovedAt_Null()
    {
        var afterSales = CreatePending();
        var sellerId = afterSales.SellerId;

        afterSales.Reject(sellerId, "quality issue");

        Assert.Equal(AfterSalesStatus.Rejected, afterSales.Status);
        Assert.Null(afterSales.ApprovedAt);
        Assert.True(afterSales.RejectedAt.HasValue);
        Assert.Equal(afterSales.RejectedAt!.Value, afterSales.RejectedAt!.Value);
    }

    [Fact]
    public void Approve_Should_Set_ApprovedAt_And_Keep_RejectedAt_Null()
    {
        var afterSales = CreatePending();
        var sellerId = afterSales.SellerId;

        afterSales.Approve(sellerId, 10m);

        Assert.Equal(AfterSalesStatus.Approved, afterSales.Status);
        Assert.True(afterSales.ApprovedAt.HasValue);
        Assert.Null(afterSales.RejectedAt);
    }
}
