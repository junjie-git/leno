using Leno.Promotion.Application.DTOs;
using Leno.Promotion.Domain.ValueObjects;

namespace Leno.Promotion.Application;

/// <summary>
/// 秒杀应用服务接口，编排运营端活动管理、买家端秒杀下单与活动查询用例。
/// 秒杀下单采用“Redis 预扣 + 异步创建订单”模式：Redis 原子扣减库存与限购，
/// 成功后发布 <c>SeckillOrderCreatedEvent</c>，订单域消费后异步创建订单。
/// </summary>
public interface ISeckillAppService
{
    // 运营端

    /// <summary>创建秒杀活动（待生效态，需 <see cref="ActivateAsync"/> 激活后初始化 Redis 库存）。</summary>
    Task<SeckillActivityDto> CreateAsync(CreateSeckillActivityDto dto, CancellationToken ct = default);

    /// <summary>
    /// 激活秒杀活动：聚合置 Active 态，并初始化 Redis 库存（总库存写入 Redis）。
    /// 仅 Pending 态可激活。
    /// </summary>
    Task ActivateAsync(Guid activityId, CancellationToken ct = default);

    /// <summary>关闭秒杀活动（终态），并将 Redis 剩余库存回写到 DB。</summary>
    Task CloseAsync(Guid activityId, CancellationToken ct = default);

    /// <summary>
    /// 关闭秒杀活动并将 Redis 剩余库存回写到 DB（用于活动结束时的库存同步）。
    /// 与 <see cref="CloseAsync"/> 的区别在于增加了库存回写步骤。
    /// </summary>
    Task CloseActivityWithStockWriteBackAsync(Guid activityId, CancellationToken ct = default);

    // 买家端

    /// <summary>
    /// 秒杀下单：校验活动进行中 → Redis 原子预扣库存 + 限购校验 → 同步 DB 基线 →
    /// 发布 <c>SeckillOrderCreatedEvent</c>（经发件箱异步派发，订单域消费后创建订单）。
    /// 下单以异步模式处理，前端凭返回的 <see cref="SeckillPlaceOrderResultDto.OrderId"/> 轮询订单域获取结果。
    /// </summary>
    Task<SeckillPlaceOrderResultDto> PlaceOrderAsync(Guid activityId, Guid userId, SeckillPlaceOrderDto dto, CancellationToken ct = default);

    // 查询

    /// <summary>获取秒杀活动详情（含 Redis 实时库存）。</summary>
    Task<SeckillActivityDto> GetByIdAsync(Guid activityId, CancellationToken ct = default);

    /// <summary>查询当前进行中的秒杀活动列表（买家侧展示）。</summary>
    Task<List<SeckillActivityDto>> GetActiveAsync(CancellationToken ct = default);

    /// <summary>
    /// 按多条件分页查询秒杀活动（运营后台），返回当前页数据与总记录数。
    /// </summary>
    /// <param name="name">名称模糊匹配关键词，null 或空白时忽略。</param>
    /// <param name="status">活动状态精确匹配，null 时忽略。</param>
    /// <param name="page">页码（从 1 开始）。</param>
    /// <param name="pageSize">每页条数。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>分页结果，包含当前页秒杀活动列表与总记录数。</returns>
    Task<SeckillListResultDto> QueryAsync(
        string? name,
        SeckillStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default);
}
