using Leno.Payment.Domain.ValueObjects;

namespace Leno.Payment.Application.DTOs;

/// <summary>
/// 对账差异 DTO。
/// </summary>
public sealed class ReconciliationDiffDto
{
    public Guid Id { get; init; }

    public DateTime BillDate { get; init; }

    public string Channel { get; init; } = string.Empty;

    public string DiffType { get; init; } = string.Empty;

    public string? ChannelTransactionNo { get; init; }

    public decimal? ChannelAmount { get; init; }

    public DateTime? ChannelTransactionTime { get; init; }

    public string? SystemTransactionNo { get; init; }

    public decimal? SystemAmount { get; init; }

    public Guid? PaymentId { get; init; }

    public string? Remark { get; init; }

    public string Status { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// 对账差异列表结果 DTO。
/// </summary>
public sealed class ReconciliationDiffListResultDto
{
    public List<ReconciliationDiffDto> Items { get; init; } = new();

    public int Total { get; init; }

    public int Page { get; init; }

    public int PageSize { get; init; }
}