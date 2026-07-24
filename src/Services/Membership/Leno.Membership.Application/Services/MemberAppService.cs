using Leno.Membership.Application.DTOs;
using Leno.Membership.Domain.Aggregates.MemberLevelDefinition;
using Leno.Membership.Domain.Exceptions;
using Leno.Membership.Domain.Repositories;
using Leno.SharedKernel.Abstractions;
using MemberAggregate = Leno.Membership.Domain.Aggregates.Member.Member;
using MemberLevelDefinitionAggregate = Leno.Membership.Domain.Aggregates.MemberLevelDefinition.MemberLevelDefinition;

namespace Leno.Membership.Application.Services;

/// <summary>
/// 会员管理应用服务实现，编排会员信息查询与运营端等级定义 CRUD 用例。
/// </summary>
public sealed class MemberAppService : IMemberAppService
{
    private readonly IMemberRepository _memberRepository;
    private readonly IMemberLevelDefinitionRepository _levelRepository;
    private readonly IUnitOfWork _unitOfWork;

    public MemberAppService(
        IMemberRepository memberRepository,
        IMemberLevelDefinitionRepository levelRepository,
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
            ?? throw new MembershipDomainException(
                $"用户 {userId} 的会员档案不存在",
                "MEMBER_NOT_FOUND");

        return ToDto(member);
    }

    /// <inheritdoc />
    public async Task<List<MemberLevelDefinitionDto>> GetLevelsAsync(CancellationToken ct = default)
    {
        var levels = await _levelRepository.GetAllAsync(ct);
        return levels.Select(ToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<MemberLevelDefinitionDto> CreateLevelAsync(CreateMemberLevelDefinitionDto dto, CancellationToken ct = default)
    {
        var level = MemberLevelDefinitionAggregate.Create(
            Guid.NewGuid(), dto.Level, dto.Name, dto.MinGrowthValue, dto.MaxGrowthValue,
            dto.Description, dto.LevelUpBonusPoints);

        await _levelRepository.AddAsync(level, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
        return ToDto(level);
    }

    /// <inheritdoc />
    public async Task<MemberLevelDefinitionDto> UpdateLevelAsync(
        Guid levelId, UpdateMemberLevelDefinitionDto dto, CancellationToken ct = default)
    {
        var level = await RequireLevelAsync(levelId, ct);
        level.Update(dto.Name, dto.MinGrowthValue, dto.MaxGrowthValue, dto.Description, dto.LevelUpBonusPoints);

        await _unitOfWork.SaveEntitiesAsync(ct);
        return ToDto(level);
    }

    private async Task<MemberLevelDefinitionAggregate> RequireLevelAsync(Guid levelId, CancellationToken ct)
        => await _levelRepository.GetByIdAsync(levelId, ct)
           ?? throw new MembershipDomainException(
               $"会员等级定义 {levelId} 不存在",
               "MEMBER_LEVEL_NOT_FOUND");

    private static MemberDto ToDto(MemberAggregate member)
        => new()
        {
            Id = member.Id,
            UserId = member.UserId,
            CurrentLevel = member.CurrentLevel,
            TotalConsumption = member.TotalConsumption,
            JoinedAt = member.JoinedAt,
            LevelUpgradedAt = member.LevelUpgradedAt,
            Status = member.Status,
            GrowthValue = member.GrowthValue,
            CurrentGrowthLevel = member.CurrentGrowthLevel
        };

    private static MemberLevelDefinitionDto ToDto(MemberLevelDefinitionAggregate level)
        => new()
        {
            Id = level.Id,
            Level = level.Level,
            Name = level.Name,
            MinGrowthValue = level.MinGrowthValue,
            MaxGrowthValue = level.MaxGrowthValue,
            Description = level.Description,
            LevelUpBonusPoints = level.LevelUpBonusPoints
        };
}
