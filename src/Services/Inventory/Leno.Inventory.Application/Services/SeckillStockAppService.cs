using Leno.Infrastructure.Abstractions;
using Leno.Inventory.Application.DTOs;
using Leno.Inventory.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Leno.Inventory.Application.Services;

/// <summary>
/// 秒杀库存应用服务实现，封装秒杀场景下的库存预扣/回退/查询用例。
/// 基于 <see cref="ISeckillStockService"/>（Redis Hash + Lua 原子层）执行库存操作，
/// 对订单取消触发的 <see cref="RestoreAsync"/> 增加幂等去重，防双重复回退。
/// </summary>
/// <remarks>
/// Promotion BC 秒杀库存迁移为遗留项，待 Promotion 规则引擎任务完成后单独迁移。
/// 当前 Inventory BC 已完整实现本服务与底层 <c>RedisSeckillStockService</c>，
/// Promotion BC 旧实现保留不动、秒杀下单命令未切换到本服务。
/// 待 Promotion 规则引擎任务完成后，将 Promotion 秒杀下单流程的库存调用切换到本接口。
/// </remarks>
public sealed class SeckillStockAppService : ISeckillStockAppService
{
    private readonly ISeckillStockService _seckillStockService;
    private readonly IIdempotencyStore _idempotencyStore;
    private readonly ILogger<SeckillStockAppService> _logger;

    public SeckillStockAppService(
        ISeckillStockService seckillStockService,
        IIdempotencyStore idempotencyStore,
        ILogger<SeckillStockAppService> logger)
    {
        ArgumentNullException.ThrowIfNull(seckillStockService);
        ArgumentNullException.ThrowIfNull(idempotencyStore);
        ArgumentNullException.ThrowIfNull(logger);
        _seckillStockService = seckillStockService;
        _idempotencyStore = idempotencyStore;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task InitializeAsync(
        Guid activityId,
        Dictionary<Guid, int> skuStocks,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(skuStocks);

        await _seckillStockService.InitializeAsync(activityId, skuStocks, ct);

        _logger.LogInformation(
            "秒杀活动库存初始化完成 ActivityId={ActivityId} SkuCount={Count}",
            activityId, skuStocks.Count);
    }

    /// <inheritdoc />
    public async Task<SeckillDeductResult> TryDeductAsync(
        Guid activityId,
        Guid skuId,
        Guid userId,
        int quantity,
        int limitPerUser,
        CancellationToken ct = default)
    {
        // TryDeduct 不需要应用层幂等：用户限购累加（INCRBY）天然防重复下单，
        // 相同用户再次调用会被限购校验拦截（Code=2）。
        var code = await _seckillStockService.TryDeductAsync(
            activityId, skuId, userId, quantity, limitPerUser, ct);

        return code switch
        {
            0 => SeckillDeductResult.Succeeded(),
            1 => SeckillDeductResult.Failed(1, $"SKU {skuId} 秒杀库存不足"),
            2 => SeckillDeductResult.Failed(2, $"用户 {userId} 超出限购上限 {limitPerUser}"),
            _ => SeckillDeductResult.Failed(code, $"秒杀预扣未知返回码 {code}")
        };
    }

    /// <inheritdoc />
    public async Task RestoreAsync(
        Guid activityId,
        Guid skuId,
        int quantity,
        Guid idempotencyKey,
        CancellationToken ct = default)
    {
        if (idempotencyKey == Guid.Empty)
        {
            // 未提供幂等键时直接执行（向后兼容无幂等场景），由底层 TotalStock 上限保护防双重复回退
            await _seckillStockService.RestoreAsync(activityId, skuId, quantity, ct);
            return;
        }

        // 幂等：相同 idempotencyKey 重复调用直接跳过
        if (await _idempotencyStore.IsProcessedAsync(idempotencyKey, ct))
        {
            _logger.LogInformation(
                "秒杀库存回退已处理，跳过重复调用 ActivityId={ActivityId} SkuId={SkuId} IdempotencyKey={Key}",
                activityId, skuId, idempotencyKey);
            return;
        }

        await _seckillStockService.RestoreAsync(activityId, skuId, quantity, ct);
        await _idempotencyStore.MarkAsProcessedAsync(idempotencyKey, ct);

        _logger.LogInformation(
            "秒杀库存回退完成 ActivityId={ActivityId} SkuId={SkuId} Quantity={Quantity} IdempotencyKey={Key}",
            activityId, skuId, quantity, idempotencyKey);
    }

    /// <inheritdoc />
    public Task<int> GetAvailableAsync(Guid activityId, Guid skuId, CancellationToken ct = default)
        => _seckillStockService.GetAvailableAsync(activityId, skuId, ct);

    /// <inheritdoc />
    public Task<Dictionary<Guid, int>> GetAllStocksAsync(Guid activityId, CancellationToken ct = default)
        => _seckillStockService.GetAllStocksAsync(activityId, ct);
}
