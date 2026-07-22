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
    private readonly IIdempotencyStore _idempotencyStore;

    public ShopAppService(
        IShopRepository shopRepository,
        ISellerProfileRepository sellerProfileRepository,
        IUnitOfWork unitOfWork,
        IFileStorageService fileStorageService,
        IValidator<SubmitShopApplicationDto> submitValidator,
        IValidator<UpdateShopInfoDto> updateShopValidator,
        IValidator<ActionReasonDto> actionReasonValidator,
        IIdempotencyStore idempotencyStore)
    {
        _shopRepository = shopRepository;
        _sellerProfileRepository = sellerProfileRepository;
        _unitOfWork = unitOfWork;
        _fileStorageService = fileStorageService;
        _submitValidator = submitValidator;
        _updateShopValidator = updateShopValidator;
        _actionReasonValidator = actionReasonValidator;
        _idempotencyStore = idempotencyStore;
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
    public async Task<ShopDto> UpdateShopInfoAsync(Guid shopId, Guid userId, UpdateShopInfoDto dto, CancellationToken ct = default)
    {
        await ValidateAsync(_updateShopValidator, dto, ct);
        EnsureNonEmptyUser(userId);

        // 归属校验：通过 userId 加载卖家所属店铺，校验 shopId 一致，防止跨卖家越权操作
        var shop = await RequireOwnedShopAsync(shopId, userId, ct);

        // 原子化更新：所有字段校验通过后再统一赋值，避免三步独立 Update 产生半更新状态
        shop.UpdateAllInfo(
            dto.ShopName,
            dto.Description,
            dto.Address,
            dto.Logo,
            dto.ContactPhone,
            dto.ContactEmail);

        await _shopRepository.UpdateAsync(shop, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        return ToShopDto(shop);
    }

    /// <inheritdoc />
    public async Task<ShopDto> UpdateMyShopInfoAsync(Guid userId, UpdateShopInfoDto dto, CancellationToken ct = default)
    {
        await ValidateAsync(_updateShopValidator, dto, ct);
        EnsureNonEmptyUser(userId);

        // 通过 sellerId 加载店铺，单次事务内完成更新与持久化，消除控制器两步操作
        var shop = await RequireShopBySellerAsync(userId, ct);

        shop.UpdateAllInfo(
            dto.ShopName,
            dto.Description,
            dto.Address,
            dto.Logo,
            dto.ContactPhone,
            dto.ContactEmail);

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
    public async Task<QualificationDto> SubmitQualificationAsync(Guid shopId, Guid userId, SubmitQualificationDto dto, Stream fileStream, string fileName, string contentType, CancellationToken ct = default)
    {
        EnsureNonEmptyUser(userId);

        // 归属校验：通过 userId 加载卖家所属店铺，校验 shopId 一致，防止跨卖家越权操作
        var shop = await RequireOwnedShopAsync(shopId, userId, ct);

        var qualification = await CreateAndUploadQualificationAsync(shop, shopId, dto, fileStream, fileName, contentType, ct);

        shop.AddQualification(qualification);
        await _shopRepository.UpdateAsync(shop, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        return ToQualificationDto(qualification);
    }

    /// <inheritdoc />
    public async Task<QualificationDto> SubmitMyQualificationAsync(Guid userId, SubmitQualificationDto dto, Stream fileStream, string fileName, string contentType, CancellationToken ct = default)
    {
        EnsureNonEmptyUser(userId);

        // 通过 sellerId 加载店铺，单次事务内完成资质创建与持久化，消除控制器两步操作
        var shop = await RequireShopBySellerAsync(userId, ct);

        // 幂等保护：若客户端提供了 IdempotencyKey，检查是否已处理，避免网络重试导致重复创建资质
        if (dto.IdempotencyKey.HasValue && dto.IdempotencyKey.Value != Guid.Empty)
        {
            var alreadyProcessed = await _idempotencyStore.IsProcessedAsync(dto.IdempotencyKey.Value, ct);
            if (alreadyProcessed)
            {
                throw new SellerShopDomainException(
                    "检测到重复提交，该资质申请正在或已完成处理，请勿重复操作",
                    "QUALIFICATION_DUPLICATE_SUBMISSION");
            }
        }

        var qualification = await CreateAndUploadQualificationAsync(shop, shop.Id, dto, fileStream, fileName, contentType, ct);

        shop.AddQualification(qualification);
        await _shopRepository.UpdateAsync(shop, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        // 业务处理成功后标记幂等键，后续相同 IdempotencyKey 的提交将被识别为重复
        if (dto.IdempotencyKey.HasValue && dto.IdempotencyKey.Value != Guid.Empty)
        {
            await _idempotencyStore.MarkAsProcessedAsync(dto.IdempotencyKey.Value, ct);
        }

        return ToQualificationDto(qualification);
    }

    private async Task<ShopQualification> CreateAndUploadQualificationAsync(
        Shop shop,
        Guid shopId,
        SubmitQualificationDto dto,
        Stream fileStream,
        string fileName,
        string contentType,
        CancellationToken ct)
    {
        var uploadResult = await _fileStorageService.UploadAsync(fileStream, fileName, contentType, "qualifications", ct);

        return ShopQualification.Create(
            Guid.NewGuid(),
            shopId,
            dto.Type,
            dto.Number,
            uploadResult.Url,
            dto.ValidFrom,
            dto.ValidTo);
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

    private async Task<Shop> RequireShopBySellerAsync(Guid sellerId, CancellationToken ct)
    {
        var shop = await _shopRepository.GetBySellerIdAsync(sellerId, ct);
        if (shop is null)
        {
            throw new SellerShopDomainException("店铺不存在", "SHOP_NOT_FOUND");
        }

        return shop;
    }

    /// <summary>
    /// 加载 userId 对应卖家拥有的店铺，并校验 shopId 归属一致。
    /// 若店铺不存在或 shopId 与卖家所属店铺不匹配，抛 SHOP_OWNERSHIP_MISMATCH 防越权。
    /// </summary>
    private async Task<Shop> RequireOwnedShopAsync(Guid shopId, Guid userId, CancellationToken ct)
    {
        var shop = await _shopRepository.GetBySellerIdAsync(userId, ct);
        if (shop is null || shop.Id != shopId)
        {
            throw new SellerShopDomainException(
                "店铺归属校验失败，当前卖家无权操作该店铺",
                "SHOP_OWNERSHIP_MISMATCH");
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
