using Leno.Promotion.Application.DTOs;
using Leno.Promotion.Domain.ValueObjects;

namespace Leno.Promotion.Application;

/// <summary>
/// 满减活动管理应用服务，编排运营端活动 CRUD 与启停用例。
/// </summary>
public interface IPromotionAppService
{
    Task<PromotionActivityDto> CreateAsync(CreatePromotionActivityDto dto, CancellationToken ct = default);

    Task<PromotionActivityDto> UpdateAsync(Guid activityId, UpdatePromotionActivityDto dto, CancellationToken ct = default);

    Task ActivateAsync(Guid activityId, CancellationToken ct = default);

    Task PauseAsync(Guid activityId, CancellationToken ct = default);

    Task CloseAsync(Guid activityId, CancellationToken ct = default);

    Task<PromotionActivityDto> GetByIdAsync(Guid activityId, CancellationToken ct = default);

    /// <summary>
    /// 按多条件分页查询满减活动（运营后台），返回当前页数据与总记录数。
    /// </summary>
    /// <param name="name">名称模糊匹配关键词，null 或空白时忽略。</param>
    /// <param name="status">活动状态精确匹配，null 时忽略。</param>
    /// <param name="startTime">活动开始时间下界（>=），null 时忽略。</param>
    /// <param name="endTime">活动结束时间上界（<=），null 时忽略。</param>
    /// <param name="page">页码（从 1 开始）。</param>
    /// <param name="pageSize">每页条数。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>分页结果，包含当前页活动列表与总记录数。</returns>
    Task<PromotionListResultDto> QueryAsync(
        string? name,
        PromotionStatus? status,
        DateTime? startTime,
        DateTime? endTime,
        int page,
        int pageSize,
        CancellationToken ct = default);
}

/// <summary>
/// 优惠券管理应用服务，编排运营端券模板 CRUD、发放与买家端领券/查询用例。
/// </summary>
public interface ICouponAppService
{
    // 运营端
    Task<CouponDto> CreateAsync(CreateCouponDto dto, CancellationToken ct = default);

    Task<CouponDto> UpdateAsync(Guid couponId, UpdateCouponDto dto, CancellationToken ct = default);

    Task EnableAsync(Guid couponId, CancellationToken ct = default);

    Task DisableAsync(Guid couponId, CancellationToken ct = default);

    Task IssueAsync(Guid couponId, int quantity, CancellationToken ct = default);

    /// <summary>
    /// 按多条件分页查询券模板（运营后台），返回当前页数据与总记录数。
    /// </summary>
    /// <param name="name">名称模糊匹配关键词，null 或空白时忽略。</param>
    /// <param name="type">券类型精确匹配，null 时忽略。</param>
    /// <param name="status">券模板状态精确匹配，null 时忽略。</param>
    /// <param name="page">页码（从 1 开始）。</param>
    /// <param name="pageSize">每页条数。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>分页结果，包含当前页券模板列表与总记录数。</returns>
    Task<CouponListResultDto> QueryAsync(
        string? name,
        CouponType? type,
        CouponTemplateStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>
    /// 按券模板标识查询详情（gRPC/REST 单条查询用）。
    /// </summary>
    Task<CouponDto?> GetByIdAsync(Guid couponId, CancellationToken ct = default);

    // 买家端
    Task<List<CouponDto>> GetReceivableAsync(CancellationToken ct = default);

    Task<UserCouponDto> ReceiveAsync(Guid userId, Guid couponId, string source, CancellationToken ct = default);

    Task<List<UserCouponDto>> GetMyCouponsAsync(Guid userId, CouponStatus? status, CancellationToken ct = default);

    /// <summary>
    /// 下单锁定优惠券（内部接口），将买家持有的指定券由 Unused 置为 Locked 并绑定 orderId。
    /// 券不存在或已被占用（非 Unused）抛 <see cref="PromotionDomainException"/>。
    /// </summary>
    Task LockCouponAsync(Guid userId, Guid couponId, Guid orderId, CancellationToken ct = default);

    /// <summary>
    /// 订单取消/释放优惠券（内部接口），将 orderId 关联的全部 Locked 用户券释放回 Unused（已过期则置为 Expired）。
    /// 无任何锁定券时幂等返回，不抛异常。
    /// </summary>
    Task ReleaseCouponsAsync(Guid orderId, CancellationToken ct = default);
}

/// <summary>锁定优惠券内部接口入参。</summary>
public sealed class LockCouponRequestDto
{
    public Guid UserId { get; set; }
    public Guid CouponId { get; set; }
    public Guid OrderId { get; set; }
}
