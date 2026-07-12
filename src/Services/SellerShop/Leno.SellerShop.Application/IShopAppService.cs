using Leno.SharedContracts.Responses;
using Leno.SellerShop.Application.DTOs;

namespace Leno.SellerShop.Application;

/// <summary>
/// 店铺管理应用服务，编排卖家入驻申请、审核、店铺信息维护与状态管理用例。
/// 事务边界由工作单元统一控制；状态流转产生的集成事件经发件箱发布。
/// </summary>
public interface IShopAppService
{
    /// <summary>卖家提交入驻申请：创建店铺与卖家档案并置待审核。</summary>
    Task<ShopDto> SubmitShopApplicationAsync(Guid userId, SubmitShopApplicationDto dto, CancellationToken ct = default);

    /// <summary>运营审核通过店铺入驻申请。</summary>
    Task ApproveShopApplicationAsync(Guid shopId, Guid reviewedBy, CancellationToken ct = default);

    /// <summary>运营驳回店铺入驻申请。</summary>
    Task RejectShopApplicationAsync(Guid shopId, Guid reviewedBy, ActionReasonDto dto, CancellationToken ct = default);

    /// <summary>卖家更新店铺基础信息、Logo 与联系方式。</summary>
    Task<ShopDto> UpdateShopInfoAsync(Guid shopId, UpdateShopInfoDto dto, CancellationToken ct = default);

    /// <summary>运营暂停店铺营业。</summary>
    Task SuspendShopAsync(Guid shopId, ActionReasonDto dto, CancellationToken ct = default);

    /// <summary>运营恢复店铺营业。</summary>
    Task ResumeShopAsync(Guid shopId, CancellationToken ct = default);

    /// <summary>关闭店铺（终态）。</summary>
    Task CloseShopAsync(Guid shopId, ActionReasonDto dto, CancellationToken ct = default);

    /// <summary>按店铺标识查询店铺信息。</summary>
    Task<ShopDto> GetShopInfoAsync(Guid shopId, CancellationToken ct = default);

    /// <summary>按卖家账号标识（用户域 UserId）查询其店铺。</summary>
    Task<ShopDto> GetMyShopAsync(Guid sellerId, CancellationToken ct = default);

    /// <summary>运营端分页查询店铺列表。</summary>
    Task<PageResult<ShopDto>> QueryShopsAsync(AdminShopQueryDto query, CancellationToken ct = default);

    /// <summary>卖家上传店铺资质。</summary>
    Task<QualificationDto> SubmitQualificationAsync(Guid shopId, SubmitQualificationDto dto, Stream fileStream, string fileName, string contentType, CancellationToken ct = default);

    /// <summary>运营查询店铺资质列表。</summary>
    Task<List<QualificationDto>> GetQualificationsAsync(Guid shopId, CancellationToken ct = default);

    /// <summary>运营审核通过资质。</summary>
    Task ApproveQualificationAsync(Guid shopId, Guid qualificationId, Guid reviewedBy, CancellationToken ct = default);

    /// <summary>运营驳回资质。</summary>
    Task RejectQualificationAsync(Guid shopId, Guid qualificationId, Guid reviewedBy, ActionReasonDto dto, CancellationToken ct = default);
}
