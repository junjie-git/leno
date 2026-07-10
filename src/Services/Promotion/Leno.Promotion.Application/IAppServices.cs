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

    Task<List<PromotionActivityDto>> QueryAsync(PromotionStatus? status, int page, int pageSize, CancellationToken ct = default);
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

    Task<List<CouponDto>> QueryAsync(CouponTemplateStatus? status, int page, int pageSize, CancellationToken ct = default);

    // 买家端
    Task<List<CouponDto>> GetReceivableAsync(CancellationToken ct = default);

    Task<UserCouponDto> ReceiveAsync(Guid userId, Guid couponId, string source, CancellationToken ct = default);

    Task<List<UserCouponDto>> GetMyCouponsAsync(Guid userId, CouponStatus? status, CancellationToken ct = default);
}
