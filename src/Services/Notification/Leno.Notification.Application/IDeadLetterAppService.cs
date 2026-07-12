using Leno.Notification.Application.DTOs;

namespace Leno.Notification.Application;

/// <summary>
/// 死信管理应用服务接口（运营端）。
/// </summary>
public interface IDeadLetterAppService
{
    /// <summary>分页查询死信列表。</summary>
    Task<DeadLetterListResultDto> GetDeadLettersAsync(int page, int pageSize, CancellationToken ct = default);

    /// <summary>批量重发死信通知。</summary>
    Task<BatchOperationResultDto> BatchResendAsync(Guid operatorId, BatchDeadLetterRequestDto request, CancellationToken ct = default);

    /// <summary>批量丢弃死信通知。</summary>
    Task<BatchOperationResultDto> BatchDiscardAsync(Guid operatorId, BatchDeadLetterRequestDto request, CancellationToken ct = default);
}