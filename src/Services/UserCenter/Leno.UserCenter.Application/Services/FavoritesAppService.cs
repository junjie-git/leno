using Leno.UserCenter.Application.DTOs;
using Leno.UserCenter.Application.Exceptions;
using Leno.UserCenter.Domain.Aggregates;
using Leno.UserCenter.Domain.Exceptions;
using Leno.UserCenter.Domain.Repositories;
using Leno.SharedKernel.Abstractions;

namespace Leno.UserCenter.Application.Services;

/// <summary>
/// 商品收藏应用服务实现，编排收藏增删查与批量操作用例。
/// 幂等语义：相同用户对相同 SPU 重复收藏返回已有记录，不重复新增。
/// 用户隔离：所有查询与删除操作均以 userId 为过滤条件，杜绝跨用户访问。
/// 从 UserAuth BC 迁入 UserCenter BC（Task A6）。
/// </summary>
public sealed class FavoritesAppService : IFavoritesAppService
{
    /// <summary>每用户收藏上限（INV-FAV-01）。</summary>
    public const int MaxFavoritesPerUser = 5000;

    /// <summary>批量操作 SPU 数量上限。</summary>
    public const int MaxBatchSize = 200;

    private readonly IFavoriteRepository _favoriteRepository;
    private readonly IUnitOfWork _unitOfWork;

    public FavoritesAppService(
        IFavoriteRepository favoriteRepository,
        IUnitOfWork unitOfWork)
    {
        _favoriteRepository = favoriteRepository;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task<PagedResult<FavoriteDto>> ListAsync(Guid userId, FavoriteQueryDto query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var safePage = query.Page < 1 ? 1 : query.Page;
        var safePageSize = query.PageSize is <= 0 or > 100 ? 20 : query.PageSize;
        var safeSort = NormalizeSort(query.Sort);
        var safeOrder = NormalizeOrder(query.Order);

        var (items, total) = await _favoriteRepository.QueryAsync(
            userId, safePage, safePageSize, safeSort, safeOrder, ct);

        var dtos = items.Select(ToDto).ToList();
        return PagedResult.Create(dtos, total, safePage, safePageSize);
    }

    /// <inheritdoc />
    public async Task<FavoriteDto> AddAsync(Guid userId, AddFavoriteDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (dto.SpuId == Guid.Empty)
        {
            throw new UserCenterValidationException("商品 SPU 标识不可为空");
        }

        // 幂等检查：已收藏直接返回
        var existing = await _favoriteRepository.GetByUserAndSpuAsync(userId, dto.SpuId, ct);
        if (existing is not null)
        {
            return ToDto(existing);
        }

        // 收藏上限校验
        var currentCount = await _favoriteRepository.CountByUserAsync(userId, ct);
        if (currentCount >= MaxFavoritesPerUser)
        {
            throw new UserCenterDomainException(
                $"每用户最多收藏 {MaxFavoritesPerUser} 件商品", "FAVORITE_LIMIT_EXCEEDED");
        }

        var favorite = Favorite.Create(Guid.NewGuid(), userId, dto.SpuId);
        await _favoriteRepository.AddAsync(favorite, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        return ToDto(favorite);
    }

    /// <inheritdoc />
    public async Task RemoveAsync(Guid userId, Guid spuId, CancellationToken ct = default)
    {
        if (spuId == Guid.Empty)
        {
            throw new UserCenterValidationException("商品 SPU 标识不可为空");
        }

        var existing = await _favoriteRepository.GetByUserAndSpuAsync(userId, spuId, ct);
        if (existing is null)
        {
            // 幂等：未收藏视为成功
            return;
        }

        await _favoriteRepository.RemoveAsync(existing, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<int> BatchDeleteAsync(Guid userId, BatchDeleteFavoritesDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (dto.SpuIds is null || dto.SpuIds.Count == 0)
        {
            throw new UserCenterValidationException("待取消收藏的 SPU 列表不可为空");
        }

        if (dto.SpuIds.Count > MaxBatchSize)
        {
            throw new UserCenterValidationException($"批量操作上限为 {MaxBatchSize} 条");
        }

        if (dto.SpuIds.Any(id => id == Guid.Empty))
        {
            throw new UserCenterValidationException("SPU 标识不可为空 GUID");
        }

        // 去重
        var distinctSpuIds = dto.SpuIds.Distinct().ToList();

        var deleted = await _favoriteRepository.BatchDeleteAsync(userId, distinctSpuIds, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        return deleted;
    }

    /// <inheritdoc />
    public async Task<FavoriteCountDto> CountAsync(Guid userId, CancellationToken ct = default)
    {
        var count = await _favoriteRepository.CountByUserAsync(userId, ct);
        return new FavoriteCountDto { Count = count };
    }

    private static string NormalizeSort(string? sort)
    {
        return sort?.Trim().ToLowerInvariant() switch
        {
            "price" => "price",
            "sales" => "sales",
            "created" => "created",
            "comprehensive" => "comprehensive",
            _ => "created"
        };
    }

    private static string NormalizeOrder(string? order)
    {
        return order?.Trim().ToLowerInvariant() switch
        {
            "asc" => "asc",
            "desc" => "desc",
            _ => "desc"
        };
    }

    private static FavoriteDto ToDto(Favorite favorite)
        => new()
        {
            FavoriteId = favorite.Id,
            SpuId = favorite.SpuId,
            FavoritedAt = favorite.FavoritedAt
        };
}
