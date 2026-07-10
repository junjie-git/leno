using Leno.SellerShop.Application.DTOs;

namespace Leno.SellerShop.Application;

/// <summary>
/// 卖家档案应用服务，编排卖家实名与资质信息的提交、更新与审核用例。
/// </summary>
public interface ISellerAppService
{
    /// <summary>提交或重新提交卖家档案审核（不存在则创建，存在则更新后提交审核）。</summary>
    Task<SellerProfileDto> SubmitSellerProfileAsync(Guid userId, SubmitSellerProfileDto dto, CancellationToken ct = default);

    /// <summary>更新卖家档案可变字段（仅草稿/已驳回态可修改）。</summary>
    Task<SellerProfileDto> UpdateSellerProfileAsync(Guid userId, SubmitSellerProfileDto dto, CancellationToken ct = default);

    /// <summary>按卖家账号标识查询卖家档案。</summary>
    Task<SellerProfileDto> GetSellerProfileAsync(Guid userId, CancellationToken ct = default);

    /// <summary>运营审核通过卖家档案。</summary>
    Task ApproveSellerProfileAsync(Guid profileId, Guid reviewedBy, CancellationToken ct = default);

    /// <summary>运营驳回卖家档案。</summary>
    Task RejectSellerProfileAsync(Guid profileId, Guid reviewedBy, ActionReasonDto dto, CancellationToken ct = default);
}
