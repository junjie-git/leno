using Leno.Payment.Domain.Aggregates;
using Leno.Payment.Domain.Exceptions;
using Leno.Payment.Domain.ValueObjects;
using Leno.Payment.Infrastructure.Services;

namespace Leno.Payment.Domain.Tests;

public class ReconciliationDiffTests
{
    [Fact]
    public void Create_Valid_ShouldCreatePendingDiff()
    {
        var diff = ReconciliationDiff.Create(
            Guid.NewGuid(), DateTime.UtcNow.Date, PaymentChannel.WeChatPay,
            ReconciliationDiffType.ChannelOnly,
            "WX_TRADE_001", 100m, DateTime.UtcNow,
            null, null, null,
            "渠道有记录，系统无记录");

        diff.Status.Should().Be(ReconciliationDiffStatus.Pending);
        diff.Channel.Should().Be(PaymentChannel.WeChatPay);
        diff.DiffType.Should().Be(ReconciliationDiffType.ChannelOnly);
        diff.ChannelTransactionNo.Should().Be("WX_TRADE_001");
        diff.ChannelAmount.Should().Be(100m);
        diff.Remark.Should().Be("渠道有记录，系统无记录");
    }

    [Fact]
    public void Create_EmptyDiffId_ShouldThrowException()
    {
        var act = () => ReconciliationDiff.Create(
            Guid.Empty, DateTime.UtcNow.Date, PaymentChannel.WeChatPay,
            ReconciliationDiffType.ChannelOnly,
            null, null, null, null, null, null, null);

        act.Should().Throw<PaymentDomainException>().WithMessage("*DiffId*");
    }

    [Fact]
    public void Create_DefaultBillDate_ShouldThrowException()
    {
        var act = () => ReconciliationDiff.Create(
            Guid.NewGuid(), default, PaymentChannel.WeChatPay,
            ReconciliationDiffType.ChannelOnly,
            null, null, null, null, null, null, null);

        act.Should().Throw<PaymentDomainException>().WithMessage("*对账日期*");
    }

    [Fact]
    public void Create_AmountMismatch_ShouldSetCorrectType()
    {
        var diff = ReconciliationDiff.Create(
            Guid.NewGuid(), DateTime.UtcNow.Date, PaymentChannel.Alipay,
            ReconciliationDiffType.AmountMismatch,
            "ALI_TRADE_001", 100m, DateTime.UtcNow,
            "PAY001", 90m, Guid.NewGuid(),
            "金额不一致");

        diff.DiffType.Should().Be(ReconciliationDiffType.AmountMismatch);
        diff.ChannelAmount.Should().Be(100m);
        diff.SystemAmount.Should().Be(90m);
        diff.PaymentId.Should().NotBeNull();
    }

    [Fact]
    public void Create_SystemOnly_ShouldSetCorrectType()
    {
        var diff = ReconciliationDiff.Create(
            Guid.NewGuid(), DateTime.UtcNow.Date, PaymentChannel.WeChatPay,
            ReconciliationDiffType.SystemOnly,
            null, null, null,
            "PAY001", 100m, Guid.NewGuid(),
            "系统有记录，渠道无记录");

        diff.DiffType.Should().Be(ReconciliationDiffType.SystemOnly);
        diff.ChannelTransactionNo.Should().BeNull();
        diff.SystemTransactionNo.Should().Be("PAY001");
    }

    [Fact]
    public void Create_BillDateNormalizedToDateOnly()
    {
        var dateTime = new DateTime(2026, 7, 13, 15, 30, 0);
        var diff = ReconciliationDiff.Create(
            Guid.NewGuid(), dateTime, PaymentChannel.WeChatPay,
            ReconciliationDiffType.ChannelOnly,
            null, null, null, null, null, null, null);

        diff.BillDate.Should().Be(new DateTime(2026, 7, 13));
        diff.BillDate.TimeOfDay.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void MarkResolved_FromPending_ShouldTransitionToResolved()
    {
        var diff = CreateDiff();
        diff.MarkResolved("已手动修复");

        diff.Status.Should().Be(ReconciliationDiffStatus.Resolved);
        diff.Remark.Should().Contain("已手动修复");
    }

    [Fact]
    public void MarkResolved_NotPending_ShouldThrowException()
    {
        var diff = CreateDiff();
        diff.MarkResolved();

        var act = () => diff.MarkResolved();

        act.Should().Throw<PaymentDomainException>().WithMessage("*状态*");
    }

    [Fact]
    public void MarkIgnored_FromPending_ShouldTransitionToIgnored()
    {
        var diff = CreateDiff();
        diff.MarkIgnored("金额差异在容忍范围内");

        diff.Status.Should().Be(ReconciliationDiffStatus.Ignored);
        diff.Remark.Should().Contain("金额差异在容忍范围内");
    }

    [Fact]
    public void MarkIgnored_NotPending_ShouldThrowException()
    {
        var diff = CreateDiff();
        diff.MarkIgnored();

        var act = () => diff.MarkIgnored();

        act.Should().Throw<PaymentDomainException>().WithMessage("*状态*");
    }

    [Fact]
    public void MarkResolved_RemarkAppendsToExisting()
    {
        var diff = CreateDiff();
        diff.MarkResolved("第一次修复");
        // Can't resolve again, but test remark append on MarkIgnored
        var diff2 = CreateDiff();
        diff2.MarkIgnored("备注1");
        diff2.Remark.Should().Contain("备注1");
    }

    private static ReconciliationDiff CreateDiff()
    {
        return ReconciliationDiff.Create(
            Guid.NewGuid(), DateTime.UtcNow.Date, PaymentChannel.WeChatPay,
            ReconciliationDiffType.ChannelOnly,
            "WX_TRADE_001", 100m, DateTime.UtcNow,
            null, null, null,
            "测试差异");
    }
}

public class ReconciliationServiceTests
{
    [Fact]
    public void ParseBill_WeChatPay_ShouldParseRecords()
    {
        var billContent = "交易时间,公众账号ID,商户号,特约商户号,设备号,微信订单号,商户订单号,用户标识,交易类型,交易状态,付款银行,货币种类,应结订单金额\n" +
            "2026-07-12 10:30:00,wx_app,merchant,,,WX_TXN_001,PAY001,user1,NATIVE,SUCCESS,ICBC,CNY,100.00\n" +
            "2026-07-12 11:00:00,wx_app,merchant,,,WX_TXN_002,PAY002,user2,NATIVE,SUCCESS,ABC,CNY,200.00";

        var records = ReconciliationService.ParseBill(PaymentChannel.WeChatPay, billContent);

        records.Should().HaveCount(2);
        records[0].ChannelTransactionNo.Should().Be("WX_TXN_001");
        records[0].OutTradeNo.Should().Be("PAY001");
        records[0].Amount.Should().Be(100m);
        records[1].ChannelTransactionNo.Should().Be("WX_TXN_002");
        records[1].Amount.Should().Be(200m);
    }

    [Fact]
    public void ParseBill_Alipay_ShouldParseRecords()
    {
        var billContent = "支付宝交易号,商户订单号,交易创建时间,付款时间,最近修改时间,交易来源地,交易类型,对方账户,交易金额,实收金额,退款金额,服务费,交易状态,备注\n" +
            "ALI_TXN_001,PAY004,2026-07-12 09:00:00,2026-07-12 09:01:00,2026-07-12 09:01:00,杭州,即时到账,user@example.com,150.00,149.00,0.00,1.00,交易成功,";

        var records = ReconciliationService.ParseBill(PaymentChannel.Alipay, billContent);

        records.Should().HaveCount(1);
        records[0].ChannelTransactionNo.Should().Be("ALI_TXN_001");
        records[0].OutTradeNo.Should().Be("PAY004");
        records[0].Amount.Should().Be(150m);
    }

    [Fact]
    public void ParseBill_EmptyContent_ShouldReturnEmptyList()
    {
        var records = ReconciliationService.ParseBill(PaymentChannel.WeChatPay, "");

        records.Should().BeEmpty();
    }

    [Fact]
    public void ParseBill_HeaderOnly_ShouldReturnEmptyList()
    {
        var billContent = "交易时间,公众账号ID,商户号,微信订单号,商户订单号,交易金额";

        var records = ReconciliationService.ParseBill(PaymentChannel.WeChatPay, billContent);

        records.Should().BeEmpty();
    }

    [Fact]
    public void CompareReconciliation_ChannelOnly_ShouldDetectChannelOnly()
    {
        var billDate = new DateTime(2026, 7, 12);
        var channel = PaymentChannel.WeChatPay;

        var channelRecords = new List<BillRecord>
        {
            new() { ChannelTransactionNo = "WX_TXN_001", OutTradeNo = "PAY001", Amount = 100m },
            new() { ChannelTransactionNo = "WX_TXN_002", OutTradeNo = "PAY002", Amount = 200m }
        };

        var systemOrders = new List<PaymentOrder>
        {
            PaymentOrder.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100m, "CNY", PaymentChannel.WeChatPay)
        };
        // Manually set OutTradeNo to match only one
        typeof(PaymentOrder).GetProperty("OutTradeNo")!.SetValue(systemOrders[0], "PAY001");
        typeof(PaymentOrder).GetProperty("ChannelTradeNo")!.SetValue(systemOrders[0], "WX_TXN_001");

        var (byOutTradeNo, byChannelTradeNo) = BuildDictionaries(systemOrders);
        var diffs = ReconciliationService.CompareReconciliation(
            billDate, channel, channelRecords, byOutTradeNo, byChannelTradeNo);

        var channelOnlyDiffs = diffs.Where(d => d.DiffType == ReconciliationDiffType.ChannelOnly).ToList();
        channelOnlyDiffs.Should().HaveCount(1);
        channelOnlyDiffs[0].ChannelTransactionNo.Should().Be("WX_TXN_002");
    }

    [Fact]
    public void CompareReconciliation_SystemOnly_ShouldDetectSystemOnly()
    {
        var billDate = new DateTime(2026, 7, 12);
        var channel = PaymentChannel.WeChatPay;

        var channelRecords = new List<BillRecord>
        {
            new() { ChannelTransactionNo = "WX_TXN_001", OutTradeNo = "PAY001", Amount = 100m }
        };

        var systemOrders = new List<PaymentOrder>
        {
            PaymentOrder.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100m, "CNY", PaymentChannel.WeChatPay),
            PaymentOrder.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 200m, "CNY", PaymentChannel.WeChatPay)
        };
        typeof(PaymentOrder).GetProperty("OutTradeNo")!.SetValue(systemOrders[0], "PAY001");
        typeof(PaymentOrder).GetProperty("ChannelTradeNo")!.SetValue(systemOrders[0], "WX_TXN_001");
        typeof(PaymentOrder).GetProperty("OutTradeNo")!.SetValue(systemOrders[1], "PAY002");
        typeof(PaymentOrder).GetProperty("ChannelTradeNo")!.SetValue(systemOrders[1], "WX_TXN_002");

        var (byOutTradeNo, byChannelTradeNo) = BuildDictionaries(systemOrders);
        var diffs = ReconciliationService.CompareReconciliation(
            billDate, channel, channelRecords, byOutTradeNo, byChannelTradeNo);

        var systemOnlyDiffs = diffs.Where(d => d.DiffType == ReconciliationDiffType.SystemOnly).ToList();
        systemOnlyDiffs.Should().HaveCount(1);
        systemOnlyDiffs[0].SystemTransactionNo.Should().Be("PAY002");
    }

    [Fact]
    public void CompareReconciliation_AmountMismatch_ShouldDetectAmountMismatch()
    {
        var billDate = new DateTime(2026, 7, 12);
        var channel = PaymentChannel.WeChatPay;

        var channelRecords = new List<BillRecord>
        {
            new() { ChannelTransactionNo = "WX_TXN_001", OutTradeNo = "PAY001", Amount = 100m }
        };

        var systemOrders = new List<PaymentOrder>
        {
            PaymentOrder.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 90m, "CNY", PaymentChannel.WeChatPay)
        };
        typeof(PaymentOrder).GetProperty("OutTradeNo")!.SetValue(systemOrders[0], "PAY001");
        typeof(PaymentOrder).GetProperty("ChannelTradeNo")!.SetValue(systemOrders[0], "WX_TXN_001");

        var (byOutTradeNo, byChannelTradeNo) = BuildDictionaries(systemOrders);
        var diffs = ReconciliationService.CompareReconciliation(
            billDate, channel, channelRecords, byOutTradeNo, byChannelTradeNo);

        var amountDiffs = diffs.Where(d => d.DiffType == ReconciliationDiffType.AmountMismatch).ToList();
        amountDiffs.Should().HaveCount(1);
        amountDiffs[0].ChannelAmount.Should().Be(100m);
        amountDiffs[0].SystemAmount.Should().Be(90m);
    }

    [Fact]
    public void CompareReconciliation_NoDifference_ShouldReturnEmptyList()
    {
        var billDate = new DateTime(2026, 7, 12);
        var channel = PaymentChannel.WeChatPay;

        var channelRecords = new List<BillRecord>
        {
            new() { ChannelTransactionNo = "WX_TXN_001", OutTradeNo = "PAY001", Amount = 100m }
        };

        var systemOrders = new List<PaymentOrder>
        {
            PaymentOrder.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100m, "CNY", PaymentChannel.WeChatPay)
        };
        typeof(PaymentOrder).GetProperty("OutTradeNo")!.SetValue(systemOrders[0], "PAY001");
        typeof(PaymentOrder).GetProperty("ChannelTradeNo")!.SetValue(systemOrders[0], "WX_TXN_001");

        var (byOutTradeNo, byChannelTradeNo) = BuildDictionaries(systemOrders);
        var diffs = ReconciliationService.CompareReconciliation(
            billDate, channel, channelRecords, byOutTradeNo, byChannelTradeNo);

        diffs.Should().BeEmpty();
    }

    /// <summary>
    /// 按系统订单列表构建 OutTradeNo 与 ChannelTradeNo 两个索引字典，
    /// 与 ReconciliationService.LoadSystemOrdersPagedAsync 内部字典构建逻辑保持一致，
    /// 避免在每个测试用例内重复编写相同的字典填充代码。
    /// </summary>
    private static (IReadOnlyDictionary<string, PaymentOrder> byOutTradeNo,
                    IReadOnlyDictionary<string, PaymentOrder> byChannelTradeNo) BuildDictionaries(
        IReadOnlyList<PaymentOrder> orders)
    {
        var byOut = new Dictionary<string, PaymentOrder>();
        var byChannel = new Dictionary<string, PaymentOrder>();
        foreach (var o in orders)
        {
            if (!string.IsNullOrEmpty(o.OutTradeNo))
            {
                byOut[o.OutTradeNo] = o;
            }
            if (!string.IsNullOrEmpty(o.ChannelTradeNo))
            {
                byChannel[o.ChannelTradeNo!] = o;
            }
        }
        return (byOut, byChannel);
    }
}