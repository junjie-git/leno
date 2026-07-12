using Leno.Payment.Domain.Exceptions;
using Leno.Payment.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.Payment.Domain.Aggregates;

/// <summary>
/// 对账差异聚合根，记录系统支付单与渠道对账单之间的差异。
/// 聚合标识 <see cref="Entity.Id"/> 即对外 <c>DiffId</c>。
/// </summary>
public sealed class ReconciliationDiff : AggregateRoot
{
    private const int MaxTransactionNoLength = 128;
    private const int MaxSystemTransactionNoLength = 128;
    private const int MaxRemarkLength = 500;

    /// <summary>对账日期（账单日期，不含时间）。</summary>
    public DateTime BillDate { get; private set; }

    /// <summary>支付渠道。</summary>
    public PaymentChannel Channel { get; private set; }

    /// <summary>差异类型。</summary>
    public ReconciliationDiffType DiffType { get; private set; }

    /// <summary>渠道账单中的交易号。</summary>
    public string? ChannelTransactionNo { get; private set; }

    /// <summary>渠道账单中的交易金额。</summary>
    public decimal? ChannelAmount { get; private set; }

    /// <summary>渠道账单中的交易时间。</summary>
    public DateTime? ChannelTransactionTime { get; private set; }

    /// <summary>系统支付单中的交易号。</summary>
    public string? SystemTransactionNo { get; private set; }

    /// <summary>系统支付单中的金额。</summary>
    public decimal? SystemAmount { get; private set; }

    /// <summary>关联的系统支付单标识。</summary>
    public Guid? PaymentId { get; private set; }

    /// <summary>差异备注。</summary>
    public string? Remark { get; private set; }

    /// <summary>修复状态。</summary>
    public ReconciliationDiffStatus Status { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private ReconciliationDiff() { }

    private ReconciliationDiff(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，创建对账差异记录。
    /// </summary>
    public static ReconciliationDiff Create(
        Guid diffId,
        DateTime billDate,
        PaymentChannel channel,
        ReconciliationDiffType diffType,
        string? channelTransactionNo,
        decimal? channelAmount,
        DateTime? channelTransactionTime,
        string? systemTransactionNo,
        decimal? systemAmount,
        Guid? paymentId,
        string? remark)
    {
        if (diffId == Guid.Empty)
        {
            throw new PaymentDomainException("DiffId 不可为空", "RECON_DIFF_ID_EMPTY");
        }

        if (billDate == default)
        {
            throw new PaymentDomainException("对账日期不可为空", "RECON_BILL_DATE_EMPTY");
        }

        if (channelTransactionNo is not null && channelTransactionNo.Length > MaxTransactionNoLength)
        {
            throw new PaymentDomainException(
                $"渠道交易号长度不可超过 {MaxTransactionNoLength} 字符", "RECON_CHANNEL_TXN_NO_LENGTH");
        }

        if (systemTransactionNo is not null && systemTransactionNo.Length > MaxSystemTransactionNoLength)
        {
            throw new PaymentDomainException(
                $"系统交易号长度不可超过 {MaxSystemTransactionNoLength} 字符", "RECON_SYSTEM_TXN_NO_LENGTH");
        }

        if (remark is not null && remark.Length > MaxRemarkLength)
        {
            throw new PaymentDomainException($"备注长度不可超过 {MaxRemarkLength} 字符", "RECON_REMARK_LENGTH");
        }

        return new ReconciliationDiff(diffId)
        {
            BillDate = billDate.Date,
            Channel = channel,
            DiffType = diffType,
            ChannelTransactionNo = channelTransactionNo,
            ChannelAmount = channelAmount,
            ChannelTransactionTime = channelTransactionTime,
            SystemTransactionNo = systemTransactionNo,
            SystemAmount = systemAmount,
            PaymentId = paymentId,
            Remark = remark,
            Status = ReconciliationDiffStatus.Pending
        };
    }

    /// <summary>
    /// 标记差异已修复。
    /// </summary>
    public void MarkResolved(string? remark = null)
    {
        if (Status != ReconciliationDiffStatus.Pending)
        {
            throw new PaymentDomainException(
                $"当前状态 {Status} 不可标记已修复，仅 Pending 可标记",
                "RECON_DIFF_RESOLVE_STATUS_INVALID");
        }

        Status = ReconciliationDiffStatus.Resolved;
        if (!string.IsNullOrWhiteSpace(remark))
        {
            Remark = (Remark is null ? "" : Remark + "; ") + remark;
        }
    }

    /// <summary>
    /// 标记差异已忽略。
    /// </summary>
    public void MarkIgnored(string? remark = null)
    {
        if (Status != ReconciliationDiffStatus.Pending)
        {
            throw new PaymentDomainException(
                $"当前状态 {Status} 不可标记已忽略，仅 Pending 可标记",
                "RECON_DIFF_IGNORE_STATUS_INVALID");
        }

        Status = ReconciliationDiffStatus.Ignored;
        if (!string.IsNullOrWhiteSpace(remark))
        {
            Remark = (Remark is null ? "" : Remark + "; ") + remark;
        }
    }
}