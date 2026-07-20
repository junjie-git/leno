using System.Globalization;
using System.Text;
using Leno.Payment.Application.Services;
using Leno.Payment.Domain.Aggregates;
using Leno.Payment.Domain.Repositories;
using Leno.Payment.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Leno.Payment.Infrastructure.Services;

/// <summary>
/// 对账服务，作为后台服务每日 T+1 自动下载渠道对账单并与系统支付单进行对账。
/// 支持微信支付和支付宝对账单下载（Mock HTTP 调用）。
/// </summary>
public sealed class ReconciliationService : BackgroundService, IReconciliationService
{
    /// <summary>
    /// 对账分页大小：每页 500 条，循环查询直到不足一页。
    /// 旧实现一次性 QueryAsync(..., 1, 10000) 在大数据量场景下会漏对账，
    /// 改为分页循环可避免该问题。
    /// </summary>
    private const int ReconciliationPageSize = 500;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReconciliationService> _logger;

    public ReconciliationService(IServiceScopeFactory scopeFactory, ILogger<ReconciliationService> logger)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(logger);
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("对账服务启动");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // 计算下次执行时间：每天凌晨 2:00（T+1 对账）
                var now = DateTime.UtcNow;
                var nextRun = now.Date.AddHours(2).AddHours(8); // UTC+8 凌晨 2:00 = UTC 18:00
                if (nextRun <= now)
                {
                    nextRun = nextRun.AddDays(1);
                }

                var delay = nextRun - now;
                _logger.LogInformation("下次对账时间：{NextRun}（{Delay} 后）", nextRun, delay);

                await Task.Delay(delay, stoppingToken);

                // 执行 T+1 对账（对账前一天的账单）
                var billDate = DateTime.UtcNow.Date.AddDays(-1);
                await ReconcileAsync(billDate, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "对账服务执行异常");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }

        _logger.LogInformation("对账服务停止");
    }

    /// <summary>
    /// 执行对账：下载渠道账单、解析、与系统支付单对比、记录差异。
    /// 系统支付单采用 PageSize=500 分页循环查询，避免一次性拉取 10000 条
    /// 在大数据量场景下漏对账。
    /// </summary>
    public async Task ReconcileAsync(DateTime billDate, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var paymentRepo = scope.ServiceProvider.GetRequiredService<IPaymentOrderRepository>();
        var diffRepo = scope.ServiceProvider.GetRequiredService<IReconciliationDiffRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        _logger.LogInformation("开始对账 日期={BillDate}", billDate.ToString("yyyy-MM-dd"));

        foreach (PaymentChannel channel in Enum.GetValues<PaymentChannel>())
        {
            try
            {
                _logger.LogInformation("处理渠道 {Channel} 对账", channel);

                // 1. 下载对账单（Mock）
                var billContent = await DownloadBillAsync(channel, billDate, ct);

                // 2. 解析对账单
                var channelRecords = ParseBill(channel, billContent);

                // 3. 分页查询系统支付单，构建 OutTradeNo 与 ChannelTradeNo 索引
                var (systemByOutTradeNo, systemByChannelTradeNo) = await LoadSystemOrdersPagedAsync(
                    paymentRepo, channel, billDate, ct);

                _logger.LogInformation(
                    "渠道 {Channel} 系统支付单加载完成 共 {Count} 条",
                    channel, systemByOutTradeNo.Count);

                // 4. 对比
                var diffs = CompareReconciliation(
                    billDate, channel, channelRecords, systemByOutTradeNo, systemByChannelTradeNo);

                // 5. 保存差异
                if (diffs.Count > 0)
                {
                    foreach (var diff in diffs)
                    {
                        await diffRepo.AddAsync(diff, ct);
                    }
                    await unitOfWork.SaveEntitiesAsync(ct);
                    _logger.LogInformation("渠道 {Channel} 对账完成，发现 {Count} 条差异", channel, diffs.Count);
                }
                else
                {
                    _logger.LogInformation("渠道 {Channel} 对账完成，无差异", channel);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "渠道 {Channel} 对账异常", channel);
            }
        }
    }

    /// <summary>
    /// 分页循环加载系统支付单并构建索引字典。
    /// 循环退出条件：返回 0 条（无更多数据）或返回不足一页（batch.Count &lt; PageSize）。
    /// 当 batch.Count == PageSize 时必须继续查询下一页，确认无更多数据。
    /// </summary>
    private async Task<(Dictionary<string, PaymentOrder> byOutTradeNo,
                        Dictionary<string, PaymentOrder> byChannelTradeNo)> LoadSystemOrdersPagedAsync(
        IPaymentOrderRepository paymentRepo,
        PaymentChannel channel,
        DateTime billDate,
        CancellationToken ct)
    {
        var byOutTradeNo = new Dictionary<string, PaymentOrder>();
        var byChannelTradeNo = new Dictionary<string, PaymentOrder>();
        var endDateExclusive = billDate.AddDays(1).AddTicks(-1);

        var page = 1;
        while (true)
        {
            var batch = await paymentRepo.QueryAsync(
                null, channel, PaymentStatus.Paid,
                billDate, endDateExclusive,
                page, ReconciliationPageSize, ct).ConfigureAwait(false);

            if (batch.Count == 0)
            {
                break;
            }

            foreach (var order in batch)
            {
                if (!string.IsNullOrEmpty(order.OutTradeNo))
                {
                    byOutTradeNo[order.OutTradeNo] = order;
                }
                if (!string.IsNullOrEmpty(order.ChannelTradeNo))
                {
                    byChannelTradeNo[order.ChannelTradeNo!] = order;
                }
            }

            if (batch.Count < ReconciliationPageSize)
            {
                break;
            }

            page++;
        }

        return (byOutTradeNo, byChannelTradeNo);
    }

    /// <summary>
    /// 下载渠道对账单（Mock HTTP 调用）。
    /// </summary>
    private async Task<string> DownloadBillAsync(PaymentChannel channel, DateTime billDate, CancellationToken ct)
    {
        _logger.LogInformation("下载 {Channel} 账单 {BillDate}", channel, billDate.ToString("yyyy-MM-dd"));

        // Mock 延迟模拟 HTTP 请求
        await Task.Delay(100, ct);

        var billDateStr = billDate.ToString("yyyy-MM-dd");

        return channel switch
        {
            PaymentChannel.WeChatPay => MockWeChatBill(billDateStr),
            PaymentChannel.Alipay => MockAlipayBill(billDateStr),
            _ => throw new NotSupportedException($"不支持的渠道：{channel}")
        };
    }

    /// <summary>
    /// Mock 微信支付对账单（CSV 格式）。
    /// 实际 API：GET /v3/bill/tradebill?bill_date={date}&bill_type=ALL
    /// </summary>
    private static string MockWeChatBill(string billDate)
    {
        var sb = new StringBuilder();
        sb.AppendLine("交易时间,公众账号ID,商户号,特约商户号,设备号,微信订单号,商户订单号,用户标识,交易类型,交易状态,付款银行,货币种类,应结订单金额,代金券金额,微信退款单号,商户退款单号,退款金额,充值券退款金额,退款类型,退款状态,商品名称,商户数据包,手续费,订单金额,申请退款金额,费率,订单优惠金额,退款优惠金额,费率备注");
        // 正常交易记录
        sb.AppendLine($"{billDate} 10:30:00,wx_app_001,merchant_001,,,WX_TRADE_001,PAY20260701000001,user_001,NATIVE,SUCCESS,ICBC,CNY,100.00,0.00,,,,,,商品A,,0.60,100.00,0.00,0.60%,0.00,0.00,");
        sb.AppendLine($"{billDate} 11:00:00,wx_app_001,merchant_001,,,WX_TRADE_002,PAY20260701000002,user_002,NATIVE,SUCCESS,ABC,CNY,200.00,0.00,,,,,,商品B,,1.20,200.00,0.00,0.60%,0.00,0.00,");
        // 仅仅在渠道有，系统没有的记录（差异）
        sb.AppendLine($"{billDate} 12:00:00,wx_app_001,merchant_001,,,WX_TRADE_003,PAY20260701000003,user_003,NATIVE,SUCCESS,BOC,CNY,50.00,0.00,,,,,,商品C,,0.30,50.00,0.00,0.60%,0.00,0.00,");
        return sb.ToString();
    }

    /// <summary>
    /// Mock 支付宝对账单（CSV 格式）。
    /// 实际 API：alipay.data.dataservice.bill.downloadurl.query
    /// </summary>
    private static string MockAlipayBill(string billDate)
    {
        var sb = new StringBuilder();
        sb.AppendLine("支付宝交易号,商户订单号,交易创建时间,付款时间,最近修改时间,交易来源地,交易类型,对方账户,交易金额,实收金额,退款金额,服务费,交易状态,备注");
        sb.AppendLine($"ALI_TRADE_001,PAY20260701000004,{billDate} 09:00:00,{billDate} 09:01:00,{billDate} 09:01:00,杭州,即时到账,user_004@example.com,150.00,149.00,0.00,1.00,交易成功,");
        sb.AppendLine($"ALI_TRADE_002,PAY20260701000005,{billDate} 10:30:00,{billDate} 10:31:00,{billDate} 10:31:00,北京,即时到账,user_005@example.com,300.00,298.00,0.00,2.00,交易成功,");
        return sb.ToString();
    }

    /// <summary>
    /// 解析对账单（CSV/TXT 格式）。
    /// </summary>
    public static List<BillRecord> ParseBill(PaymentChannel channel, string billContent)
    {
        var records = new List<BillRecord>();
        if (string.IsNullOrWhiteSpace(billContent)) return records;

        var lines = billContent.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length <= 1) return records; // 只有表头，无数据

        // 跳过表头行
        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;

            var fields = line.Split(',');

            var record = channel switch
            {
                PaymentChannel.WeChatPay => ParseWeChatBillRecord(fields),
                PaymentChannel.Alipay => ParseAlipayBillRecord(fields),
                _ => null
            };

            if (record is not null)
            {
                records.Add(record);
            }
        }

        return records;
    }

    private static BillRecord? ParseWeChatBillRecord(string[] fields)
    {
        if (fields.Length < 6) return null;

        // 微信账单字段顺序：交易时间,公众账号ID,商户号,...,微信订单号(索引5),商户订单号(索引6),用户标识,交易类型,交易状态,付款银行,货币种类,应结订单金额(索引12),...
        // 金额在索引 12（总金额）
        var transactionNo = fields.Length > 5 ? fields[5].Trim() : string.Empty;
        var outTradeNo = fields.Length > 6 ? fields[6].Trim() : string.Empty;
        var amountStr = fields.Length > 23 ? fields[23].Trim() : (fields.Length > 12 ? fields[12].Trim() : "0");
        var timeStr = fields.Length > 0 ? fields[0].Trim() : string.Empty;

        if (string.IsNullOrWhiteSpace(transactionNo)) return null;

        _ = decimal.TryParse(amountStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount);
        _ = DateTime.TryParse(timeStr, out var transactionTime);

        return new BillRecord
        {
            ChannelTransactionNo = transactionNo,
            OutTradeNo = outTradeNo,
            Amount = amount,
            TransactionTime = transactionTime
        };
    }

    private static BillRecord? ParseAlipayBillRecord(string[] fields)
    {
        if (fields.Length < 4) return null;

        // 支付宝账单字段顺序：支付宝交易号(索引0),商户订单号(索引1),交易创建时间,付款时间,最近修改时间,交易来源地,交易类型,对方账户,交易金额(索引8),实收金额,退款金额,服务费,交易状态,备注
        var transactionNo = fields[0].Trim();
        var outTradeNo = fields.Length > 1 ? fields[1].Trim() : string.Empty;
        var amountStr = fields.Length > 8 ? fields[8].Trim() : "0";
        var timeStr = fields.Length > 3 ? fields[3].Trim() : string.Empty;

        if (string.IsNullOrWhiteSpace(transactionNo)) return null;

        _ = decimal.TryParse(amountStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount);
        _ = DateTime.TryParse(timeStr, out var transactionTime);

        return new BillRecord
        {
            ChannelTransactionNo = transactionNo,
            OutTradeNo = outTradeNo,
            Amount = amount,
            TransactionTime = transactionTime
        };
    }

    /// <summary>
    /// 对比渠道账单与系统支付单，生成差异列表。
    /// 接收预构建的索引字典（由 ReconcileAsync 分页加载后构建），避免在比对阶段重复扫描系统订单列表。
    /// 匹配优先级：先按 OutTradeNo 匹配，未命中再按 ChannelTradeNo 匹配。
    /// </summary>
    /// <param name="billDate">对账日期（账单日期）。</param>
    /// <param name="channel">支付渠道。</param>
    /// <param name="channelRecords">渠道账单解析后的交易记录列表。</param>
    /// <param name="systemByOutTradeNo">系统支付单按 OutTradeNo 建立的索引。</param>
    /// <param name="systemByChannelTradeNo">系统支付单按 ChannelTradeNo 建立的索引。</param>
    /// <returns>差异列表，可能包含 ChannelOnly / SystemOnly / AmountMismatch。</returns>
    public static List<ReconciliationDiff> CompareReconciliation(
        DateTime billDate,
        PaymentChannel channel,
        IReadOnlyList<BillRecord> channelRecords,
        IReadOnlyDictionary<string, PaymentOrder> systemByOutTradeNo,
        IReadOnlyDictionary<string, PaymentOrder> systemByChannelTradeNo)
    {
        var diffs = new List<ReconciliationDiff>();
        var matchedOrderIds = new HashSet<Guid>();

        // 1. 遍历渠道账单，按 OutTradeNo / ChannelTradeNo 匹配系统支付单
        foreach (var record in channelRecords)
        {
            PaymentOrder? matched = null;

            // 优先按商户订单号匹配
            if (!string.IsNullOrEmpty(record.OutTradeNo)
                && systemByOutTradeNo.TryGetValue(record.OutTradeNo, out var byOut))
            {
                matched = byOut;
            }
            // 其次按渠道交易号匹配
            else if (!string.IsNullOrEmpty(record.ChannelTransactionNo)
                && systemByChannelTradeNo.TryGetValue(record.ChannelTransactionNo, out var byChannel))
            {
                matched = byChannel;
            }

            if (matched is null)
            {
                // 渠道有记录，系统无匹配 → ChannelOnly
                diffs.Add(ReconciliationDiff.Create(
                    Guid.NewGuid(), billDate, channel, ReconciliationDiffType.ChannelOnly,
                    record.ChannelTransactionNo, record.Amount, record.TransactionTime,
                    null, null, null,
                    $"渠道有交易记录但系统无对应支付单：商户订单号={record.OutTradeNo}"));
            }
            else
            {
                matchedOrderIds.Add(matched.Id);

                // 金额不一致 → AmountMismatch
                if (matched.Amount != record.Amount)
                {
                    diffs.Add(ReconciliationDiff.Create(
                        Guid.NewGuid(), billDate, channel, ReconciliationDiffType.AmountMismatch,
                        record.ChannelTransactionNo, record.Amount, record.TransactionTime,
                        matched.OutTradeNo, matched.Amount, matched.Id,
                        $"金额不一致：渠道={record.Amount}，系统={matched.Amount}"));
                }
            }
        }

        // 2. 系统有记录但渠道未匹配 → SystemOnly
        foreach (var kvp in systemByOutTradeNo)
        {
            var order = kvp.Value;
            if (matchedOrderIds.Contains(order.Id))
            {
                continue;
            }

            diffs.Add(ReconciliationDiff.Create(
                Guid.NewGuid(), billDate, channel, ReconciliationDiffType.SystemOnly,
                order.ChannelTradeNo, null, null,
                order.OutTradeNo, order.Amount, order.Id,
                $"系统有支付单但渠道无对应交易记录"));
        }

        return diffs;
    }
}

/// <summary>
/// 对账单解析后的交易记录。
/// </summary>
public sealed class BillRecord
{
    /// <summary>渠道交易号。</summary>
    public string ChannelTransactionNo { get; init; } = string.Empty;

    /// <summary>商户订单号。</summary>
    public string OutTradeNo { get; init; } = string.Empty;

    /// <summary>交易金额。</summary>
    public decimal Amount { get; init; }

    /// <summary>交易时间。</summary>
    public DateTime TransactionTime { get; init; }
}
