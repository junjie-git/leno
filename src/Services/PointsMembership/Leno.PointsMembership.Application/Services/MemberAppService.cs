using Leno.PointsMembership.Application.DTOs;
using Leno.PointsMembership.Domain.Exceptions;
using Leno.PointsMembership.Domain.Repositories;
using Leno.SharedKernel.Abstractions;
using MemberAggregate = Leno.PointsMembership.Domain.Aggregates.Member;
using MembershipLevelAggregate = Leno.PointsMembership.Domain.Aggregates.MembershipLevel;

namespace Leno.PointsMembership.Application.Services;

/// <summary>
/// 会员管理应用服务实现，编排会员信息查询与运营端等级 CRUD、启停用例。
/// </summary>
public sealed class MemberAppService : IMemberAppService
{
    private readonly IMemberRepository _memberRepository;
    private readonly IMembershipLevelRepository _levelRepository;
    private readonly IUnitOfWork _unitOfWork;

    public MemberAppService(
        IMemberRepository memberRepository,
        IMembershipLevelRepository levelRepository,
        IUnitOfWork unitOfWork)
    {
        _memberRepository = memberRepository;
        _levelRepository = levelRepository;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task<MemberDto> GetMemberInfoAsync(Guid userId, CancellationToken ct = default)
    {
        var member = await _memberRepository.GetByUserIdAsync(userId, ct)
            ?? throw new PointsDomainException(
                $"用户 {userId} 的会员档案不存在",
                "MEMBER_NOT_FOUND",
                404);

        return ToDto(member);
    }

    /// <inheritdoc />
    public async Task<List<MembershipLevelDto>> GetLevelsAsync(CancellationToken ct = default)
    {
        var levels = await _levelRepository.GetAllAsync(ct);
        return levels.Select(ToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<MembershipLevelDto> CreateLevelAsync(CreateMembershipLevelDto dto, CancellationToken ct = default)
    {
        var level = MembershipLevelAggregate.Create(
            Guid.NewGuid(), dto.Name, dto.Level, dto.MinConsumption, dto.DiscountRate);

        await _levelRepository.AddAsync(level, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
        return ToDto(level);
    }

    /// <inheritdoc />
    public async Task<MembershipLevelDto> UpdateLevelAsync(Guid levelId, UpdateMembershipLevelDto dto, CancellationToken ct = default)
    {
        var level = await RequireLevelAsync(levelId, ct);
        level.Update(dto.Name, level.Level, dto.MinConsumption, dto.DiscountRate);

        await _unitOfWork.SaveEntitiesAsync(ct);
        return ToDto(level);
    }

    /// <inheritdoc />
    public async Task EnableLevelAsync(Guid levelId, CancellationToken ct = default)
    {
        var level = await RequireLevelAsync(levelId, ct);
        level.Enable();
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    /// <inheritdoc />
    public async Task DisableLevelAsync(Guid levelId, CancellationToken ct = default)
    {
        var level = await RequireLevelAsync(levelId, ct);
        level.Disable();
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    private async Task<MembershipLevelAggregate> RequireLevelAsync(Guid levelId, CancellationToken ct)
        => await _levelRepository.GetByIdAsync(levelId, ct)
           ?? throw new PointsDomainException(
               $"会员等级 {levelId} 不存在",
               "LEVEL_NOT_FOUND",
               404);

    private static MemberDto ToDto(MemberAggregate member)
        => new()
        {
            Id = member.Id,
            UserId = member.UserId,
            CurrentLevel = member.CurrentLevel,
            TotalConsumption = member.TotalConsumption,
            JoinedAt = member.JoinedAt,
            LevelUpgradedAt = member.LevelUpgradedAt,
            Status = member.Status
        };

    private static MembershipLevelDto ToDto(MembershipLevelAggregate level)
        => new()
        {
            Id = level.Id,
            Name = level.Name,
            Level = level.Level,
            MinConsumption = level.MinConsumption,
            DiscountRate = level.DiscountRate,
            Status = level.Status
        };
}
