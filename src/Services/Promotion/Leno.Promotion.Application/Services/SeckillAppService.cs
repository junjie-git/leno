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
/// 秒杀下单采用"Redis 预扣 + 异步创建订单"模式，保证高并发下的库存安全与最终一致性。
/// 支持多 SKU 库存管理，Redis 使用 Hash 结构存储。
/// </summary>
public sealed class SeckillAppService : ISeckillAppService
{
    private readonly ISeckillActivityRepository _repository;
    private readonly ISeckillStockService _stockService;
    private readonly ISeckillPreOccupationRecordRepository _preOccupationRecordRepository;
    private readonly IUnitOfWork _unitOfWork;

    public SeckillAppService(
        ISeckillActivityRepository repository,
        ISeckillStockService stockService,
        ISeckillPreOccupationRecordRepository preOccupationRecordRepository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _stockService = stockService;
        _preOccupationRecordRepository = preOccupationRecordRepository;
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

        // 先初始化 Redis 库存，成功后再改聚合状态，避免 Redis 故障期间聚合被错误标记为 Active
        // （原实现先 activity.Activate() 再 InitializeAsync，若 Redis 异常则内存中 Status 已为 Active 但未持久化；
        //  若 Redis 半成功（HashSetAsync 覆盖已有库存），老库存被重置，后续 PlaceOrder 用错误库存）
        var skuStocks = new Dictionary<Guid, int>
        {
            { activity.SkuId, activity.TotalStock }
        };
        try
        {
            await _stockService.InitializeAsync(activity.Id, skuStocks, ct);
        }
        catch (Exception ex)
        {
            throw new PromotionDomainException(
                $"秒杀活动 {activityId} Redis 库存初始化失败：{ex.Message}", "SECKILL_REDIS_INIT_FAILED", ex);
        }

        activity.Activate();
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
    public async Task CloseActivityWithStockWriteBackAsync(Guid activityId, CancellationToken ct = default)
    {
        var activity = await RequireActivityAsync(activityId, ct);
        activity.Close();

        // 活动关闭时，将 Redis 剩余库存回写到 DB
        await _stockService.WriteBackToDbAsync(activityId, ct);

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

        // 使用请求中的 SkuId，若未指定则使用活动的默认 SkuId（向后兼容）
        var skuId = dto.SkuId != Guid.Empty ? dto.SkuId : activity.SkuId;

        // 1. Redis 原子预扣库存 + 限购校验（高频热路径，支持多 SKU）
        var deductResult = await _stockService.TryDeductAsync(
            activity.Id, skuId, userId, dto.Quantity, activity.LimitPerUser, ct);

        if (deductResult != 0)
        {
            var reason = deductResult switch
            {
                1 => "库存不足",
                2 => "超出限购",
                _ => "未知错误"
            };
            throw new PromotionDomainException(
                $"秒杀失败：{reason}", "SECKILL_DEDUCT_FAILED");
        }

        // 2. Redis 预扣成功后仅创建预占记录 + 发事件（不调用 activity.DeductStock）
        // DB 基线（AvailableStock）由后台对账任务或活动结束时 WriteBackToDbAsync 同步，
        // 避免 rowversion 乐观锁冲突导致"幽灵失败"（高并发下 N 个请求通过 Redis Lua 原子扣减，
        // 但 DB 提交只能串行，除第一个外其余均因 rowversion 不匹配抛 DbUpdateConcurrencyException 被回退 Redis）
        // 热路径仅写预占记录 + 发件箱事件，DB 不参与扣减
        Guid orderId;
        try
        {
            orderId = Guid.NewGuid();
            activity.RecordOrderCreated(userId, orderId, dto.Quantity);

            // 创建预占记录，供补偿任务跟踪履约状态
            var preOccupationRecord = SeckillPreOccupationRecord.Create(
                activity.Id, skuId, userId, orderId, dto.Quantity);
            await _preOccupationRecordRepository.AddAsync(preOccupationRecord, ct);

            await _unitOfWork.SaveEntitiesAsync(ct);
        }
        catch
        {
            // 预占记录写入失败（非乐观锁冲突，如网络故障），回退 Redis 预扣保持库存最终一致
            await _stockService.RestoreAsync(activity.Id, skuId, dto.Quantity, CancellationToken.None);
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
        if (activities.Count == 0)
        {
            return new List<SeckillActivityDto>();
        }

        // 并行调用 ToDtoAsync（内部含 Redis 往返），将 N 次串行改为 N 次并行，
        // 利用 Redis 连接池并发能力降低列表查询累积延迟（N+1 → 并行 N）
        var dtoTasks = activities.Select(a => ToDtoAsync(a, ct)).ToArray();
        var dtos = await Task.WhenAll(dtoTasks);
        return dtos.ToList();
    }

    /// <inheritdoc />
    public async Task<List<SeckillActivityDto>> QueryAsync(SeckillStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        var activities = await _repository.GetByStatusAsync(status, page, pageSize, ct);
        if (activities.Count == 0)
        {
            return new List<SeckillActivityDto>();
        }

        // 并行调用 ToDtoAsync（内部含 Redis 往返），将 N 次串行改为 N 次并行
        var dtoTasks = activities.Select(a => ToDtoAsync(a, ct)).ToArray();
        var dtos = await Task.WhenAll(dtoTasks);
        return dtos.ToList();
    }

    private async Task<SeckillActivityAggregate> RequireActivityAsync(Guid activityId, CancellationToken ct)
        => await _repository.GetByIdAsync(activityId, ct)
           ?? throw new PromotionDomainException($"秒杀活动 {activityId} 不存在", "SECKILL_NOT_FOUND");

    /// <summary>
    /// 转换为 DTO 并填充 Redis 实时库存。Active 态读取 Redis，非 Active 态实时库存同 DB 基线。
    /// </summary>
    private async Task<SeckillActivityDto> ToDtoAsync(SeckillActivityAggregate activity, CancellationToken ct)
    {
        var realtimeStock = activity.Status == SeckillStatus.Active
            ? await _stockService.GetAvailableAsync(activity.Id, activity.SkuId, ct)
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