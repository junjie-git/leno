using Leno.PointsMembership.Application.DTOs;
using Leno.PointsMembership.Domain.Aggregates;
using Leno.PointsMembership.Domain.Exceptions;
using Leno.PointsMembership.Domain.Repositories;
using Leno.SharedKernel.Abstractions;
using MembershipPackageAggregate = Leno.PointsMembership.Domain.Aggregates.MembershipPackage;
using UserMembershipAggregate = Leno.PointsMembership.Domain.Aggregates.UserMembership;

namespace Leno.PointsMembership.Application.Services;

/// <summary>
/// 会员套餐管理应用服务实现，编排套餐查询与运营端 CRUD、启停及用户订阅用例。
/// </summary>
public sealed class MembershipPackageAppService : IMembershipPackageAppService
{
    private readonly IMembershipPackageRepository _packageRepository;
    private readonly IUserMembershipRepository _userMembershipRepository;
    private readonly IUnitOfWork _unitOfWork;

    public MembershipPackageAppService(
        IMembershipPackageRepository packageRepository,
        IUserMembershipRepository userMembershipRepository,
        IUnitOfWork unitOfWork)
    {
        _packageRepository = packageRepository;
        _userMembershipRepository = userMembershipRepository;
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
    public async Task<MembershipPackageDto> UpdatePackageAsync(Guid packageId, UpdateMembershipPackageDto dto, CancellationToken ct = default)
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
    public async Task SubscribeAsync(Guid userId, Guid packageId, CancellationToken ct = default)
    {
        var package = await RequirePackageAsync(packageId, ct);

        // 创建待支付的用户会员权益记录，实际订单创建转发至订单域。
        var userMembership = UserMembershipAggregate.Create(
            Guid.NewGuid(), userId, package.Id, package.Level);

        await _userMembershipRepository.AddAsync(userMembership, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    private async Task<MembershipPackageAggregate> RequirePackageAsync(Guid packageId, CancellationToken ct)
        => await _packageRepository.GetByIdAsync(packageId, ct)
           ?? throw new PointsDomainException(
               $"会员套餐 {packageId} 不存在",
               "PACKAGE_NOT_FOUND",
               404);

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
