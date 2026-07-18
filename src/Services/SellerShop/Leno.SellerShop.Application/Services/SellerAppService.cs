using FluentValidation;
using Leno.SellerShop.Application.DTOs;
using Leno.SellerShop.Application.Exceptions;
using Leno.SellerShop.Domain.Aggregates;
using Leno.SellerShop.Domain.Exceptions;
using Leno.SellerShop.Domain.Repositories;
using Leno.SharedKernel.Abstractions;

namespace Leno.SellerShop.Application.Services;

/// <summary>
/// 卖家档案应用服务实现，编排实名与资质信息的提交、更新与审核用例。
/// </summary>
public sealed class SellerAppService : ISellerAppService
{
    private readonly ISellerProfileRepository _profileRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<SubmitSellerProfileDto> _submitProfileValidator;
    private readonly IValidator<ActionReasonDto> _actionReasonValidator;

    public SellerAppService(
        ISellerProfileRepository profileRepository,
        IUnitOfWork unitOfWork,
        IValidator<SubmitSellerProfileDto> submitProfileValidator,
        IValidator<ActionReasonDto> actionReasonValidator)
    {
        _profileRepository = profileRepository;
        _unitOfWork = unitOfWork;
        _submitProfileValidator = submitProfileValidator;
        _actionReasonValidator = actionReasonValidator;
    }

    /// <inheritdoc />
    public async Task<SellerProfileDto> SubmitSellerProfileAsync(Guid userId, SubmitSellerProfileDto dto, CancellationToken ct = default)
    {
        await ValidateAsync(_submitProfileValidator, dto, ct);
        EnsureNonEmptyUser(userId);

        var profile = await _profileRepository.GetByUserIdAsync(userId, ct);
        if (profile is null)
        {
            profile = SellerProfile.Create(
                Guid.NewGuid(),
                userId,
                dto.RealName,
                dto.IdCard,
                dto.BusinessLicenseNo,
                dto.BankAccount);
            await _profileRepository.AddAsync(profile, ct);
        }
        else
        {
            profile.Update(dto.RealName, dto.IdCard, dto.BusinessLicenseNo, dto.BankAccount);
            await _profileRepository.UpdateAsync(profile, ct);
        }

        profile.SubmitForVerification();
        await _unitOfWork.SaveEntitiesAsync(ct);

        return ToProfileDto(profile);
    }

    /// <inheritdoc />
    public async Task<SellerProfileDto> UpdateSellerProfileAsync(Guid userId, SubmitSellerProfileDto dto, CancellationToken ct = default)
    {
        await ValidateAsync(_submitProfileValidator, dto, ct);
        EnsureNonEmptyUser(userId);

        var profile = await RequireProfileByUserIdAsync(userId, ct);
        profile.Update(dto.RealName, dto.IdCard, dto.BusinessLicenseNo, dto.BankAccount);

        await _profileRepository.UpdateAsync(profile, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        return ToProfileDto(profile);
    }

    /// <inheritdoc />
    public async Task<SellerProfileDto> GetSellerProfileAsync(Guid userId, CancellationToken ct = default)
    {
        EnsureNonEmptyUser(userId);
        var profile = await RequireProfileByUserIdAsync(userId, ct);
        return ToProfileDto(profile);
    }

    /// <inheritdoc />
    public async Task ApproveSellerProfileAsync(Guid profileId, Guid reviewedBy, CancellationToken ct = default)
    {
        var profile = await RequireProfileByIdAsync(profileId, ct);
        profile.Approve(reviewedBy);
        await _profileRepository.UpdateAsync(profile, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    /// <inheritdoc />
    public async Task RejectSellerProfileAsync(Guid profileId, Guid reviewedBy, ActionReasonDto dto, CancellationToken ct = default)
    {
        await ValidateAsync(_actionReasonValidator, dto, ct);
        var profile = await RequireProfileByIdAsync(profileId, ct);
        profile.Reject(reviewedBy, dto.Reason);
        await _profileRepository.UpdateAsync(profile, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    private async Task<SellerProfile> RequireProfileByUserIdAsync(Guid userId, CancellationToken ct)
    {
        var profile = await _profileRepository.GetByUserIdAsync(userId, ct);
        if (profile is null)
        {
            throw new SellerShopDomainException("卖家档案不存在", "SELLER_NOT_FOUND");
        }

        return profile;
    }

    private async Task<SellerProfile> RequireProfileByIdAsync(Guid profileId, CancellationToken ct)
    {
        var profile = await _profileRepository.GetByIdAsync(profileId, ct);
        if (profile is null)
        {
            throw new SellerShopDomainException("卖家档案不存在", "SELLER_NOT_FOUND");
        }

        return profile;
    }

    private static void EnsureNonEmptyUser(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new SellerShopDomainException("卖家账号标识不可为空", "SELLER_USER_EMPTY");
        }
    }

    private static async Task ValidateAsync<T>(IValidator<T> validator, T instance, CancellationToken ct)
    {
        var result = await validator.ValidateAsync(instance, ct);
        if (!result.IsValid)
        {
            throw new SellerShopValidationException(result.Errors.Select(e => e.ErrorMessage));
        }
    }

    private static SellerProfileDto ToProfileDto(SellerProfile profile)
        => new()
        {
            Id = profile.Id,
            UserId = profile.UserId,
            RealName = profile.RealName,
            IdCard = profile.IdCard,
            BusinessLicenseNo = profile.BusinessLicenseNo,
            BankAccount = profile.BankAccount,
            Status = profile.Status,
            ReviewedBy = profile.ReviewedBy,
            StatusReason = profile.StatusReason,
            CreatedAt = profile.CreatedAt,
            UpdatedAt = profile.UpdatedAt
        };
}
