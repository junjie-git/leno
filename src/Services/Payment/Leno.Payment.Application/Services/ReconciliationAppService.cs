using Leno.Payment.Application.DTOs;
using Leno.Payment.Application.Services;
using Leno.Payment.Domain.Aggregates;
using Leno.Payment.Domain.Repositories;
using Leno.Payment.Domain.ValueObjects;

namespace Leno.Payment.Application.Services;

/// <summary>
/// 对账应用服务实现。
/// </summary>
public sealed class ReconciliationAppService : IReconciliationAppService
{
    private readonly IReconciliationDiffRepository _diffRepository;
    private readonly IReconciliationService _reconciliationService;

    public ReconciliationAppService(
        IReconciliationDiffRepository diffRepository,
        IReconciliationService reconciliationService)
    {
        ArgumentNullException.ThrowIfNull(diffRepository);
        ArgumentNullException.ThrowIfNull(reconciliationService);
        _diffRepository = diffRepository;
        _reconciliationService = reconciliationService;
    }

    /// <inheritdoc />
    public async Task<ReconciliationDiffListResultDto> QueryDiffsAsync(
        DateTime? billDate,
        PaymentChannel? channel,
        ReconciliationDiffType? diffType,
        ReconciliationDiffStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var diffs = await _diffRepository.QueryAsync(billDate, channel, diffType, status, page, pageSize, ct);
        var total = await _diffRepository.CountAsync(billDate, channel, diffType, status, ct);

        var items = diffs.Select(ToDto).ToList();

        return new ReconciliationDiffListResultDto
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <inheritdoc />
    public async Task TriggerReconciliationAsync(DateTime billDate, CancellationToken ct = default)
    {
        await _reconciliationService.ReconcileAsync(billDate, ct);
    }

    private static ReconciliationDiffDto ToDto(ReconciliationDiff diff)
        => new()
        {
            Id = diff.Id,
            BillDate = diff.BillDate,
            Channel = diff.Channel.ToString(),
            DiffType = diff.DiffType.ToString(),
            ChannelTransactionNo = diff.ChannelTransactionNo,
            ChannelAmount = diff.ChannelAmount,
            ChannelTransactionTime = diff.ChannelTransactionTime,
            SystemTransactionNo = diff.SystemTransactionNo,
            SystemAmount = diff.SystemAmount,
            PaymentId = diff.PaymentId,
            Remark = diff.Remark,
            Status = diff.Status.ToString(),
            CreatedAt = diff.CreatedAt
        };
}