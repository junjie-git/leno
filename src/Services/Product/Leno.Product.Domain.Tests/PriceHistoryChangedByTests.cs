using FluentAssertions;
using Leno.Product.Domain.Aggregates;
using Xunit;

namespace Leno.Product.Domain.Tests;

/// <summary>
/// P1-T13 单元测试：验证 <see cref="PriceHistory.Create"/> 的 changedBy 参数与 <see cref="PriceHistory.ChangedBy"/> 属性。
/// 修复审计 #13：原 Create 不接受 changedBy 参数，ToPriceChangeRecordDto 硬编码 ChangedBy = string.Empty。
/// </summary>
public class PriceHistoryChangedByTests
{
    /// <summary>
    /// 传入 changedBy 时，ChangedBy 属性应正确存储（trim 后）。
    /// </summary>
    [Fact]
    public void Create_WithChangedBy_ShouldStoreChangedBy()
    {
        var history = PriceHistory.Create(
            Guid.NewGuid(), Guid.NewGuid(), 100m, 90m,
            reason: "促销调价", currency: "CNY", changedBy: "seller-001");

        history.ChangedBy.Should().Be("seller-001");
    }

    /// <summary>
    /// 传入带空白的 changedBy 时，应 trim 后存储。
    /// </summary>
    [Fact]
    public void Create_WithWhitespaceChangedBy_ShouldTrim()
    {
        var history = PriceHistory.Create(
            Guid.NewGuid(), Guid.NewGuid(), 100m, 90m,
            changedBy: "  seller-001  ");

        history.ChangedBy.Should().Be("seller-001");
    }

    /// <summary>
    /// 不传 changedBy（默认空字符串）时，ChangedBy 应为空字符串（向后兼容）。
    /// </summary>
    [Fact]
    public void Create_WithoutChangedBy_DefaultsToEmpty()
    {
        var history = PriceHistory.Create(
            Guid.NewGuid(), Guid.NewGuid(), 100m, 90m);

        history.ChangedBy.Should().BeEmpty();
    }

    /// <summary>
    /// 传入空白 changedBy 时，应规范化为空字符串。
    /// </summary>
    [Fact]
    public void Create_WithEmptyChangedBy_NormalizesToEmpty()
    {
        var history = PriceHistory.Create(
            Guid.NewGuid(), Guid.NewGuid(), 100m, 90m,
            changedBy: "   ");

        history.ChangedBy.Should().BeEmpty();
    }

    /// <summary>
    /// 同时传入 changedBy 和 reason 时，两者都应正确存储。
    /// </summary>
    [Fact]
    public void Create_WithChangedByAndReason_ShouldStoreBoth()
    {
        var history = PriceHistory.Create(
            Guid.NewGuid(), Guid.NewGuid(), 100m, 80m,
            reason: "双 11 促销", currency: "CNY", changedBy: "admin-002");

        history.ChangedBy.Should().Be("admin-002");
        history.Reason.Should().Be("双 11 促销");
    }
}
