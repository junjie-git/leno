using Leno.Promotion.Application.DTOs;
using Leno.Promotion.Domain.Aggregates;
using Leno.Promotion.Domain.Exceptions;
using Leno.Promotion.Domain.Repositories;
using Leno.Promotion.Domain.Services;
using Leno.Promotion.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using SeckillActivityAggregate = Leno.Promotion.Domain.Aggregates.SeckillActivity;

namespace Leno.Promotion.Application.Services;

/// <summary>
/// 秒杀应用服务实现。
/// 秒杀下单采用“Redis 预扣 + 异步创建订单”模式，保证高并发下的库存安全与最终一致性。
/// </summary>
public sealed class SeckillAppService : ISeckillAppService
{
    private readonly ISeckillActivityRepository _repository;
    private readonly ISeckillStockService _stockService;
    private readonly IUnitOfWork _unitOfWork;

    public SeckillAppService(
        ISeckillActivityRepository repository,
        ISeckillStockService stockService,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _stockService = stockService;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task<SeckillActivityDto> CreateAsync(CreateSeckillActivityDto dto, CancellationToken ct = default)
    {
        var activity = SeckillActivityAggregate.Create(
            Guid.NewGuid(),
            dto.SpuId,
            dto.SkuId,
            dto.SeckillPrice,
            dto.OriginalPrice,
            dto.TotalStock,
            dto.LimitPerUser,
            dto.StartTime,
            dto.EndTime);

        await _repository.AddAsync(activity, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
        return await ToDtoAsync(activity, ct);
    }

    /// <inheritdoc />
    public async Task ActivateAsync(Guid activityId, CancellationToken ct = default)
    {
        var activity = await RequireActivityAsync(activityId, ct);
        activity.Activate();

        // 初始化 Redis 库存（总库存写入 Redis，供秒杀下单原子预扣）
        await _stockService.InitializeAsync(activity.Id, activity.TotalStock, ct);

        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    /// <inheritdoc />
    public async Task CloseAsync(Guid activityId, CancellationToken ct = default)
    {
        var activity = await RequireActivityAsync(activityId, ct);
        activity.Close();
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<SeckillPlaceOrderResultDto> PlaceOrderAsync(
        Guid activityId,
        Guid userId,
        SeckillPlaceOrderDto dto,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (userId == Guid.Empty)
        {
            throw new PromotionDomainException("UserId 不可为空", "SECKILL_USER_EMPTY");
        }

        if (dto.Quantity <= 0)
        {
            throw new PromotionDomainException("下单数量须大于 0", "SECKILL_QTY_INVALID");
        }

        var activity = await RequireActivityAsync(activityId, ct);

        // 1. Redis 原子预扣库存 + 限购校验（高频路径）
        var deducted = await _stockService.TryDeductAsync(
            activity.Id, userId, dto.Quantity, activity.LimitPerUser, ct);

        if (!deducted)
        {
            throw new PromotionDomainException(
                "秒杀失败：库存不足或超出限购", "SECKILL_DEDUCT_FAILED");
        }

        // 2. Redis 预扣成功后同步 DB 基线并发布事件；若 DB 失败则回退 Redis
        Guid orderId;
        try
        {
            activity.DeductStock(userId, dto.Quantity);

            orderId = Guid.NewGuid();
            activity.RecordOrderCreated(userId, orderId, dto.Quantity);

            await _unitOfWork.SaveEntitiesAsync(ct);
        }
        catch
        {
            // DB 写入失败，回退 Redis 预扣，保持库存最终一致
            await _stockService.RestoreAsync(activity.Id, userId, dto.Quantity, CancellationToken.None);
            throw;
        }

        return new SeckillPlaceOrderResultDto
        {
            OrderId = orderId,
            ActivityId = activity.Id,
            UserId = userId,
            SeckillPrice = activity.SeckillPrice,
            Quantity = dto.Quantity,
            PlacedAt = DateTime.UtcNow
        };
    }

    /// <inheritdoc />
    public async Task<SeckillActivityDto> GetByIdAsync(Guid activityId, CancellationToken ct = default)
    {
        var activity = await RequireActivityAsync(activityId, ct);
        return await ToDtoAsync(activity, ct);
    }

    /// <inheritdoc />
    public async Task<List<SeckillActivityDto>> GetActiveAsync(CancellationToken ct = default)
    {
        var activities = await _repository.GetActiveAsync(DateTime.UtcNow, ct);
        var dtos = new List<SeckillActivityDto>(activities.Count);
        foreach (var activity in activities)
        {
            dtos.Add(await ToDtoAsync(activity, ct));
        }
        return dtos;
    }

    /// <inheritdoc />
    public async Task<List<SeckillActivityDto>> QueryAsync(SeckillStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        var activities = await _repository.GetByStatusAsync(status, page, pageSize, ct);
        var dtos = new List<SeckillActivityDto>(activities.Count);
        foreach (var activity in activities)
        {
            dtos.Add(await ToDtoAsync(activity, ct));
        }
        return dtos;
    }

    private async Task<SeckillActivityAggregate> RequireActivityAsync(Guid activityId, CancellationToken ct)
        => await _repository.GetByIdAsync(activityId, ct)
           ?? throw new PromotionDomainException($"秒杀活动 {activityId} 不存在", "SECKILL_NOT_FOUND", 404);

    /// <summary>
    /// 转换为 DTO 并填充 Redis 实时库存。Active 态读取 Redis，非 Active 态实时库存同 DB 基线。
    /// </summary>
    private async Task<SeckillActivityDto> ToDtoAsync(SeckillActivityAggregate activity, CancellationToken ct)
    {
        var realtimeStock = activity.Status == SeckillStatus.Active
            ? await _stockService.GetAvailableAsync(activity.Id, ct)
            : activity.AvailableStock;

        return new SeckillActivityDto
        {
            Id = activity.Id,
            SpuId = activity.SpuId,
            SkuId = activity.SkuId,
            SeckillPrice = activity.SeckillPrice,
            OriginalPrice = activity.OriginalPrice,
            TotalStock = activity.TotalStock,
            AvailableStock = activity.AvailableStock,
            AvailableStockRealtime = realtimeStock,
            LimitPerUser = activity.LimitPerUser,
            StartTime = activity.StartTime,
            EndTime = activity.EndTime,
            Status = activity.Status,
            CreatedAt = activity.CreatedAt
        };
    }
}
