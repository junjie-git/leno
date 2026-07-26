using Leno.UserCenter.Application.DTOs;
using Leno.UserCenter.Application.Exceptions;
using Leno.UserCenter.Domain.Aggregates;
using Leno.UserCenter.Domain.Exceptions;
using Leno.UserCenter.Domain.Repositories;
using Leno.SharedKernel.Abstractions;

namespace Leno.UserCenter.Application.Services;

/// <summary>
/// 浏览历史应用服务实现，编排浏览历史记录、查询、删除与清空用例。
/// 幂等语义：相同用户对相同 SPU 在 <see cref="RevisitWindow"/> 内重复浏览仅更新 ViewedAt，不新增记录。
/// 用户隔离：所有查询与删除操作均以 userId 为过滤条件，杜绝跨用户访问。
/// 从 UserAuth BC 迁入 UserCenter BC（Task A6）。
/// </summary>
public sealed class BrowseHistoryAppService : IBrowseHistoryAppService
{
    /// <summary>重复浏览窗口：5 秒内同一 SPU 视为重复浏览，仅更新时间不新增记录（INV-BH-01）。</summary>
    public static readonly TimeSpan RevisitWindow = TimeSpan.FromSeconds(5);

    /// <summary>每用户浏览历史上限（INV-BH-02）：超出时自动删除最旧记录。</summary>
    public const int MaxHistoryPerUser = 1000;

    /// <summary>批量操作 ID 数量上限。</summary>
    public const int MaxBatchSize = 200;

    private readonly IBrowseHistoryRepository _historyRepository;
    private readonly IUnitOfWork _unitOfWork;

    public BrowseHistoryAppService(
        IBrowseHistoryRepository historyRepository,
        IUnitOfWork unitOfWork)
    {
        _historyRepository = historyRepository;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task<PagedResult<BrowseHistoryDto>> ListAsync(Guid userId, BrowseHistoryQueryDto query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var safePage = query.Page < 1 ? 1 : query.Page;
        var safePageSize = query.PageSize is <= 0 or > 100 ? 20 : query.PageSize;

        var (items, total) = await _historyRepository.QueryAsync(userId, safePage, safePageSize, ct);

        var dtos = items.Select(ToDto).ToList();
        return PagedResult.Create(dtos, total, safePage, safePageSize);
    }

    /// <inheritdoc />
    public async Task<BrowseHistoryDto> AddAsync(Guid userId, AddBrowseHistoryDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (dto.SpuId == Guid.Empty)
        {
            throw new UserCenterValidationException("商品 SPU 标识不可为空");
        }

        if (dto.SkuId.HasValue && dto.SkuId.Value == Guid.Empty)
        {
            throw new UserCenterValidationException("商品 SKU 标识不可为空 GUID");
        }

        var now = DateTime.UtcNow;
        var existing = await _historyRepository.FindLatestByUserAndSpuAsync(userId, dto.SpuId, ct);

        // 幂等：5 秒内同一 SPU 仅更新 ViewedAt，不新增记录
        if (existing is not null && (now - existing.ViewedAt) < RevisitWindow)
        {
            existing.MarkRevisited(now);
            await _historyRepository.UpdateAsync(existing, ct);
            await _unitOfWork.SaveEntitiesAsync(ct);
            return ToDto(existing);
        }

        // 容量自愈：超上限时由仓储层在新增后清理最旧记录（事务内）
        var currentCount = await _historyRepository.QueryAsync(userId, 1, 1, ct);
        if (currentCount.Total >= MaxHistoryPerUser)
        {
            // 删除最旧的一批记录（按 viewed_at 升序取前 N 条）
            // 此处通过 BatchDeleteAsync 反向实现：查询第 MaxHistoryPerUser 条之前的记录并删除
            // 简化实现：仅在仓储层增加 TrimAsync 由 SaveEntities 触发，本处只标记需清理
            // 但本域采用直接删除最旧一条的简化策略，避免引入额外仓储方法
            await TrimOldestAsync(userId, ct);
        }

        var history = BrowseHistory.Create(Guid.NewGuid(), userId, dto.SpuId, dto.SkuId, now);
        await _historyRepository.AddAsync(history, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        return ToDto(history);
    }

    /// <inheritdoc />
    public async Task RemoveAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        if (id == Guid.Empty)
        {
            throw new UserCenterValidationException("浏览历史标识不可为空");
        }

        var history = await _historyRepository.GetByIdAsync(id, ct);
        if (history is null)
        {
            throw new UserCenterDomainException("浏览历史不存在", "BROWSE_HISTORY_NOT_FOUND");
        }

        if (history.UserId != userId)
        {
            throw new UserCenterDomainException("无权操作他人浏览历史", "BROWSE_HISTORY_FORBIDDEN");
        }

        await _historyRepository.RemoveAsync(history, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<int> BatchDeleteAsync(Guid userId, BatchDeleteBrowseHistoryDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (dto.Ids is null || dto.Ids.Count == 0)
        {
            throw new UserCenterValidationException("待删除的浏览历史 ID 列表不可为空");
        }

        if (dto.Ids.Count > MaxBatchSize)
        {
            throw new UserCenterValidationException($"批量操作上限为 {MaxBatchSize} 条");
        }

        if (dto.Ids.Any(id => id == Guid.Empty))
        {
            throw new UserCenterValidationException("浏览历史标识不可为空 GUID");
        }

        var distinctIds = dto.Ids.Distinct().ToList();

        var deleted = await _historyRepository.BatchDeleteAsync(userId, distinctIds, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        return deleted;
    }

    /// <inheritdoc />
    public async Task<int> ClearAllAsync(Guid userId, CancellationToken ct = default)
    {
        var deleted = await _historyRepository.ClearAllByUserAsync(userId, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
        return deleted;
    }

    /// <summary>
    /// 容量自愈：查询最旧的一条历史并删除，使总条数回落到上限以内。
    /// </summary>
    private async Task TrimOldestAsync(Guid userId, CancellationToken ct)
    {
        var (oldest, _) = await _historyRepository.QueryAsync(userId, MaxHistoryPerUser, 1, ct);
        if (oldest.Count > 0)
        {
            var oldestIds = oldest.Select(h => h.Id).ToList();
            await _historyRepository.BatchDeleteAsync(userId, oldestIds, ct);
        }
    }

    private static BrowseHistoryDto ToDto(BrowseHistory history)
        => new()
        {
            HistoryId = history.Id,
            SpuId = history.SpuId,
            SkuId = history.SkuId,
            ViewedAt = history.ViewedAt
        };
}
