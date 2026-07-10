using FluentValidation;
using Leno.UserAuth.Application.DTOs;
using Leno.UserAuth.Application.Exceptions;
using Leno.UserAuth.Domain.Aggregates;
using Leno.UserAuth.Domain.Exceptions;
using Leno.UserAuth.Domain.Repositories;
using Leno.SharedKernel.Abstractions;

namespace Leno.UserAuth.Application.Services;

/// <summary>
/// 收货地址应用服务实现，编排地址增删改查与默认地址切换。
/// 校验地址归属与上限，默认地址唯一不变量在事务内协调保证。
/// </summary>
public sealed class AddressAppService : IAddressAppService
{
    /// <summary>每用户 Active 地址上限（INV-05）。</summary>
    public const int MaxAddressesPerUser = 20;

    private readonly IAddressRepository _addressRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<SaveAddressDto> _addressValidator;

    public AddressAppService(
        IAddressRepository addressRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IValidator<SaveAddressDto> addressValidator)
    {
        _addressRepository = addressRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _addressValidator = addressValidator;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AddressDto>> ListAsync(Guid userId, CancellationToken ct = default)
    {
        var addresses = await _addressRepository.GetActiveByUserIdAsync(userId, ct);
        return addresses
            .OrderByDescending(a => a.IsDefault)
            .ThenByDescending(a => a.UpdatedAt)
            .Select(ToAddressDto)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<AddressDto> CreateAsync(Guid userId, SaveAddressDto dto, CancellationToken ct = default)
    {
        await ValidateAsync(dto, ct);

        var activeCount = await _addressRepository.CountActiveByUserIdAsync(userId, ct);
        if (activeCount >= MaxAddressesPerUser)
        {
            throw new UserAuthDomainException(
                $"每用户最多 {MaxAddressesPerUser} 条地址", "ADDRESS_LIMIT_EXCEEDED", 409);
        }

        // 首条地址自动置默认（INV-06、AC-27）
        var shouldBeDefault = dto.IsDefault || activeCount == 0;

        if (shouldBeDefault)
        {
            await ClearExistingDefaultAsync(userId, ct);
        }

        var address = Address.Create(
            Guid.NewGuid(),
            userId,
            dto.RecipientName,
            dto.RecipientPhone,
            dto.Province,
            dto.City,
            dto.District,
            dto.Detail,
            dto.Tag,
            shouldBeDefault);

        await _addressRepository.AddAsync(address, ct);

        if (shouldBeDefault)
        {
            await UpdateUserDefaultAddressAsync(userId, address.Id, ct);
        }

        await _unitOfWork.SaveEntitiesAsync(ct);
        return ToAddressDto(address);
    }

    /// <inheritdoc />
    public async Task<AddressDto> UpdateAsync(Guid userId, Guid addressId, SaveAddressDto dto, CancellationToken ct = default)
    {
        await ValidateAsync(dto, ct);
        var address = await RequireOwnedAddressAsync(userId, addressId, ct);

        address.UpdateInfo(
            dto.RecipientName,
            dto.RecipientPhone,
            dto.Province,
            dto.City,
            dto.District,
            dto.Detail,
            dto.Tag);

        await _addressRepository.UpdateAsync(address, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        return ToAddressDto(address);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid userId, Guid addressId, CancellationToken ct = default)
    {
        var address = await RequireOwnedAddressAsync(userId, addressId, ct);
        var wasDefault = address.IsDefault;

        address.SoftDelete();
        await _addressRepository.UpdateAsync(address, ct);

        // 删除默认地址后自愈：选取最近一条 Active 为默认，无则置空（INV-15、AC-29）
        if (wasDefault)
        {
            var remaining = await _addressRepository.GetActiveByUserIdAsync(userId, ct);
            var nextDefault = remaining
                .OrderByDescending(a => a.UpdatedAt)
                .FirstOrDefault();

            if (nextDefault is not null)
            {
                nextDefault.MarkAsDefault();
                await _addressRepository.UpdateAsync(nextDefault, ct);
                await UpdateUserDefaultAddressAsync(userId, nextDefault.Id, ct);
            }
            else
            {
                await UpdateUserDefaultAddressAsync(userId, null, ct);
            }
        }

        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<AddressDto> SetDefaultAsync(Guid userId, Guid addressId, CancellationToken ct = default)
    {
        var address = await RequireOwnedAddressAsync(userId, addressId, ct);

        await ClearExistingDefaultAsync(userId, ct);
        address.MarkAsDefault();

        await _addressRepository.UpdateAsync(address, ct);
        await UpdateUserDefaultAddressAsync(userId, address.Id, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        return ToAddressDto(address);
    }

    private async Task ClearExistingDefaultAsync(Guid userId, CancellationToken ct)
    {
        var addresses = await _addressRepository.GetActiveByUserIdAsync(userId, ct);
        foreach (var existing in addresses.Where(a => a.IsDefault))
        {
            existing.UnmarkDefault();
            await _addressRepository.UpdateAsync(existing, ct);
        }
    }

    private async Task UpdateUserDefaultAddressAsync(Guid userId, Guid? addressId, CancellationToken ct)
    {
        var user = await _userRepository.GetByIdAsync(userId, ct);
        if (user is null)
        {
            throw new UserAuthDomainException("用户不存在", "USER_NOT_FOUND", 404);
        }

        user.SetDefaultAddress(addressId);
        await _userRepository.UpdateAsync(user, ct);
    }

    private async Task<Address> RequireOwnedAddressAsync(Guid userId, Guid addressId, CancellationToken ct)
    {
        var address = await _addressRepository.GetByIdAsync(addressId, ct);
        if (address is null)
        {
            throw new UserAuthDomainException("地址不存在", "ADDRESS_NOT_FOUND", 404);
        }

        if (address.UserId != userId)
        {
            throw new UserAuthDomainException("无权操作他人地址", "ADDRESS_FORBIDDEN", 403);
        }

        return address;
    }

    private async Task ValidateAsync(SaveAddressDto dto, CancellationToken ct)
    {
        var result = await _addressValidator.ValidateAsync(dto, ct);
        if (!result.IsValid)
        {
            throw new UserAuthValidationException(result.Errors.Select(e => e.ErrorMessage));
        }
    }

    private static AddressDto ToAddressDto(Address address)
        => new()
        {
            Id = address.Id,
            UserId = address.UserId,
            RecipientName = address.RecipientName,
            RecipientPhone = address.RecipientPhone,
            Province = address.Province,
            City = address.City,
            District = address.District,
            Detail = address.Detail,
            Tag = address.Tag,
            IsDefault = address.IsDefault,
            Status = address.Status,
            CreatedAt = address.CreatedAt,
            UpdatedAt = address.UpdatedAt
        };
}
