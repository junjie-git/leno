using Leno.Membership.Application.DTOs;
using Leno.Membership.Domain.Aggregates.MembershipPackage;
using Leno.Membership.Domain.Exceptions;
using Leno.Membership.Domain.Repositories;
using Leno.Membership.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using MembershipPackageAggregate = Leno.Membership.Domain.Aggregates.MembershipPackage.MembershipPackage;

namespace Leno.Membership.Application.Services;

/// <summary>
/// 会员套餐管理应用服务实现，编排套餐查询与运营端 CRUD、启停、买家订阅用例。
/// </summary>
public sealed class MembershipPackageAppService : IMembershipPackageAppService
{
    private readonly IMembershipPackageRepository _packageRepository;
    private readonly IUnitOfWork _unitOfWork;

    public MembershipPackageAppService(
        IMembershipPackageRepository packageRepository,
        IUnitOfWork unitOfWork)
    {
        _packageRepository = packageRepository;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task<List<MembershipPackageDto>> GetPackagesAsync(CancellationToken ct = default)
    {
        var packages = await _packageRepository.GetAllEnabledAsync(ct);
        return packages.Select(ToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<MembershipPackageDto> CreatePackageAsync(CreateMembershipPackageDto dto, CancellationToken ct = default)
    {
        var package = MembershipPackageAggregate.Create(
            Guid.NewGuid(), dto.Name, dto.Level, dto.Price, dto.DurationDays, dto.Benefits);

        await _packageRepository.AddAsync(package, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
        return ToDto(package);
    }

    /// <inheritdoc />
    public async Task<MembershipPackageDto> UpdatePackageAsync(
        Guid packageId, UpdateMembershipPackageDto dto, CancellationToken ct = default)
    {
        var package = await RequirePackageAsync(packageId, ct);
        package.Update(dto.Name, package.Level, dto.Price, dto.DurationDays, dto.Benefits);

        await _unitOfWork.SaveEntitiesAsync(ct);
        return ToDto(package);
    }

    /// <inheritdoc />
    public async Task EnablePackageAsync(Guid packageId, CancellationToken ct = default)
    {
        var package = await RequirePackageAsync(packageId, ct);
        package.Enable();
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    /// <inheritdoc />
    public async Task DisablePackageAsync(Guid packageId, CancellationToken ct = default)
    {
        var package = await RequirePackageAsync(packageId, ct);
        package.Disable();
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<SubscriptionResultDto> SubscribeAsync(
        Guid userId, Guid packageId, CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
        {
            throw new MembershipDomainException("UserId 不可为空", "MEMBER_USER_EMPTY");
        }

        var package = await RequirePackageAsync(packageId, ct);

        if (package.Status != PackageStatus.Enabled)
        {
            throw new MembershipDomainException(
                $"会员套餐 {packageId} 已停用，不可订阅",
                "PACKAGE_DISABLED");
        }

        // 生成待支付订阅意图，承载套餐快照。实际订单创建转发至订单域，
        // 订单域据此订阅标识创建支付单，支付成功后回调激活会员权益。
        return new SubscriptionResultDto
        {
            SubscriptionId = Guid.NewGuid(),
            UserId = userId,
            PackageId = package.Id,
            PackageName = package.Name,
            Level = package.Level,
            Price = package.Price,
            DurationDays = package.DurationDays,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };
    }

    private async Task<MembershipPackageAggregate> RequirePackageAsync(Guid packageId, CancellationToken ct)
        => await _packageRepository.GetByIdAsync(packageId, ct)
           ?? throw new MembershipDomainException(
               $"会员套餐 {packageId} 不存在",
               "PACKAGE_NOT_FOUND");

    private static MembershipPackageDto ToDto(MembershipPackageAggregate package)
        => new()
        {
            Id = package.Id,
            Name = package.Name,
            Level = package.Level,
            Price = package.Price,
            DurationDays = package.DurationDays,
            Benefits = package.Benefits,
            Status = package.Status
        };
}
