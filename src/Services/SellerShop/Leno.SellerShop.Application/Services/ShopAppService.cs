using FluentValidation;
using Leno.SellerShop.Application.DTOs;
using Leno.SellerShop.Application.Exceptions;
using Leno.SellerShop.Domain.Aggregates;
using Leno.SellerShop.Domain.Entities;
using Leno.SellerShop.Domain.Exceptions;
using Leno.SellerShop.Domain.Repositories;
using Leno.SellerShop.Domain.ValueObjects;
using Leno.SharedContracts.Responses;
using Leno.SharedKernel.Abstractions;
using Leno.Infrastructure.Abstractions;

namespace Leno.SellerShop.Application.Services;

/// <summary>
/// 店铺管理应用服务实现。
/// 编排入驻申请、审核、信息维护与状态管理，事务边界由工作单元统一控制，
/// 状态流转产生的集成事件经发件箱与聚合变更同事务发布。
/// </summary>
public sealed class ShopAppService : IShopAppService
{
    private readonly IShopRepository _shopRepository;
    private readonly ISellerProfileRepository _sellerProfileRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFileStorageService _fileStorageService;
    private readonly IValidator<SubmitShopApplicationDto> _submitValidator;
    private readonly IValidator<UpdateShopInfoDto> _updateShopValidator;
    private readonly IValidator<ActionReasonDto> _actionReasonValidator;

    public ShopAppService(
        IShopRepository shopRepository,
        ISellerProfileRepository sellerProfileRepository,
        IUnitOfWork unitOfWork,
        IFileStorageService fileStorageService,
        IValidator<SubmitShopApplicationDto> submitValidator,
        IValidator<UpdateShopInfoDto> updateShopValidator,
        IValidator<ActionReasonDto> actionReasonValidator)
    {
        _shopRepository = shopRepository;
        _sellerProfileRepository = sellerProfileRepository;
        _unitOfWork = unitOfWork;
        _fileStorageService = fileStorageService;
        _submitValidator = submitValidator;
        _updateShopValidator = updateShopValidator;
        _actionReasonValidator = actionReasonValidator;
    }

    /// <inheritdoc />
    public async Task<ShopDto> SubmitShopApplicationAsync(Guid userId, SubmitShopApplicationDto dto, CancellationToken ct = default)
    {
        await ValidateAsync(_submitValidator, dto, ct);
        EnsureNonEmptyUser(userId);

        var existing = await _shopRepository.GetBySellerIdAsync(userId, ct);
        if (existing is not null)
        {
            throw new SellerShopDomainException("该账号已提交入驻申请", "SHOP_ALREADY_EXISTS");
        }

        var shop = Shop.Create(
            Guid.NewGuid(),
            userId,
            dto.ShopName,
            dto.ContactPhone,
            dto.ContactEmail,
            dto.Description,
            dto.Logo,
            dto.BusinessLicenseNo,
            dto.Address);

        var profile = SellerProfile.Create(
            Guid.NewGuid(),
            userId,
            dto.RealName,
            dto.IdCard,
            dto.BusinessLicenseNo,
            dto.BankAccount);
        profile.SubmitForVerification();

        await _shopRepository.AddAsync(shop, ct);
        await _sellerProfileRepository.AddAsync(profile, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        return ToShopDto(shop);
    }

    /// <inheritdoc />
    public async Task ApproveShopApplicationAsync(Guid shopId, Guid reviewedBy, CancellationToken ct = default)
    {
        var shop = await RequireShopAsync(shopId, ct);
        shop.Approve(reviewedBy);
        await _shopRepository.UpdateAsync(shop, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    /// <inheritdoc />
    public async Task RejectShopApplicationAsync(Guid shopId, Guid reviewedBy, ActionReasonDto dto, CancellationToken ct = default)
    {
        await ValidateAsync(_actionReasonValidator, dto, ct);
        var shop = await RequireShopAsync(shopId, ct);
        shop.Reject(reviewedBy, dto.Reason);
        await _shopRepository.UpdateAsync(shop, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<ShopDto> UpdateShopInfoAsync(Guid shopId, UpdateShopInfoDto dto, CancellationToken ct = default)
    {
        await ValidateAsync(_updateShopValidator, dto, ct);
        var shop = await RequireShopAsync(shopId, ct);

        shop.UpdateInfo(dto.ShopName, dto.Description, dto.Address);
        shop.UpdateLogo(dto.Logo);
        shop.UpdateContact(dto.ContactPhone, dto.ContactEmail);

        await _shopRepository.UpdateAsync(shop, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        return ToShopDto(shop);
    }

    /// <inheritdoc />
    public async Task SuspendShopAsync(Guid shopId, ActionReasonDto dto, CancellationToken ct = default)
    {
        await ValidateAsync(_actionReasonValidator, dto, ct);
        var shop = await RequireShopAsync(shopId, ct);
        shop.Suspend(dto.Reason);
        await _shopRepository.UpdateAsync(shop, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    /// <inheritdoc />
    public async Task ResumeShopAsync(Guid shopId, CancellationToken ct = default)
    {
        var shop = await RequireShopAsync(shopId, ct);
        shop.Resume();
        await _shopRepository.UpdateAsync(shop, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    /// <inheritdoc />
    public async Task CloseShopAsync(Guid shopId, ActionReasonDto dto, CancellationToken ct = default)
    {
        await ValidateAsync(_actionReasonValidator, dto, ct);
        var shop = await RequireShopAsync(shopId, ct);
        shop.Close(dto.Reason);
        await _shopRepository.UpdateAsync(shop, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<ShopDto> GetShopInfoAsync(Guid shopId, CancellationToken ct = default)
    {
        var shop = await RequireShopAsync(shopId, ct);
        return ToShopDto(shop);
    }

    /// <inheritdoc />
    public async Task<ShopDto> GetMyShopAsync(Guid sellerId, CancellationToken ct = default)
    {
        EnsureNonEmptyUser(sellerId);
        var shop = await _shopRepository.GetBySellerIdAsync(sellerId, ct);
        if (shop is null)
        {
            throw new SellerShopDomainException("店铺不存在", "SHOP_NOT_FOUND");
        }

        return ToShopDto(shop);
    }

    /// <inheritdoc />
    public async Task<PageResult<ShopDto>> QueryShopsAsync(AdminShopQueryDto query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        ShopStatus? status = null;
        if (!string.IsNullOrWhiteSpace(query.Status)
            && Enum.TryParse<ShopStatus>(query.Status, ignoreCase: true, out var statusValue))
        {
            status = statusValue;
        }

        var (items, total) = await _shopRepository.QueryAsync(status, query.Keyword, query.Page, query.PageSize, ct);
        var dtos = items.Select(ToShopDto).ToList();

        var safePage = query.Page < 1 ? 1 : query.Page;
        var safePageSize = query.PageSize is <= 0 or > 100 ? 20 : query.PageSize;

        return new PageResult<ShopDto>(dtos, total, safePage, safePageSize);
    }

    /// <inheritdoc />
    public async Task<QualificationDto> SubmitQualificationAsync(Guid shopId, SubmitQualificationDto dto, Stream fileStream, string fileName, string contentType, CancellationToken ct = default)
    {
        var shop = await RequireShopAsync(shopId, ct);

        var uploadResult = await _fileStorageService.UploadAsync(fileStream, fileName, contentType, "qualifications", ct);

        var qualification = ShopQualification.Create(
            Guid.NewGuid(),
            shopId,
            dto.Type,
            dto.Number,
            uploadResult.Url,
            dto.ValidFrom,
            dto.ValidTo);

        shop.AddQualification(qualification);
        await _shopRepository.UpdateAsync(shop, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        return ToQualificationDto(qualification);
    }

    /// <inheritdoc />
    public async Task<List<QualificationDto>> GetQualificationsAsync(Guid shopId, CancellationToken ct = default)
    {
        var shop = await RequireShopAsync(shopId, ct);
        return shop.Qualifications.Select(ToQualificationDto).ToList();
    }

    /// <inheritdoc />
    public async Task ApproveQualificationAsync(Guid shopId, Guid qualificationId, Guid reviewedBy, CancellationToken ct = default)
    {
        var shop = await RequireShopAsync(shopId, ct);
        shop.ApproveQualification(qualificationId, reviewedBy);
        await _shopRepository.UpdateAsync(shop, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    /// <inheritdoc />
    public async Task RejectQualificationAsync(Guid shopId, Guid qualificationId, Guid reviewedBy, ActionReasonDto dto, CancellationToken ct = default)
    {
        await ValidateAsync(_actionReasonValidator, dto, ct);
        var shop = await RequireShopAsync(shopId, ct);
        shop.RejectQualification(qualificationId, reviewedBy, dto.Reason);
        await _shopRepository.UpdateAsync(shop, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    private async Task<Shop> RequireShopAsync(Guid shopId, CancellationToken ct)
    {
        var shop = await _shopRepository.GetByIdAsync(shopId, ct);
        if (shop is null)
        {
            throw new SellerShopDomainException("店铺不存在", "SHOP_NOT_FOUND");
        }

        return shop;
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

    private static ShopDto ToShopDto(Shop shop)
        => new()
        {
            Id = shop.Id,
            SellerId = shop.SellerId,
            ShopName = shop.ShopName,
            Logo = shop.Logo,
            Description = shop.Description,
            ContactPhone = shop.ContactPhone,
            ContactEmail = shop.ContactEmail,
            BusinessLicenseNo = shop.BusinessLicenseNo,
            Address = shop.Address,
            Status = shop.Status,
            ProductCount = shop.ProductCount,
            StatusReason = shop.StatusReason,
            ReviewedBy = shop.ReviewedBy,
            CreatedAt = shop.CreatedAt,
            UpdatedAt = shop.UpdatedAt,
            Qualifications = shop.Qualifications.Select(ToQualificationDto).ToList()
        };

    private static QualificationDto ToQualificationDto(ShopQualification qualification)
        => new()
        {
            Id = qualification.Id,
            ShopId = qualification.ShopId,
            Type = qualification.Type,
            Number = qualification.Number,
            ImageUrl = qualification.ImageUrl,
            ValidFrom = qualification.ValidFrom,
            ValidTo = qualification.ValidTo,
            Status = qualification.Status,
            RejectReason = qualification.RejectReason,
            ReviewedBy = qualification.ReviewedBy,
            CreatedAt = qualification.CreatedAt
        };
}
