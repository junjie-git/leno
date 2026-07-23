using Leno.Promotion.Domain.Aggregates;
using Leno.Promotion.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using PromotionRuleDefinitionAggregate = Leno.Promotion.Domain.Aggregates.PromotionRuleDefinition;

namespace Leno.Promotion.Infrastructure.Repositories;

/// <summary>
/// 促销规则定义 EF Core 仓储实现。
/// </summary>
public sealed class EfCorePromotionRuleDefinitionRepository : IPromotionRuleDefinitionRepository
{
    private readonly PromotionDbContext _context;

    public EfCorePromotionRuleDefinitionRepository(PromotionDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public async Task<PromotionRuleDefinitionAggregate?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.PromotionRuleDefinitions.FirstOrDefaultAsync(r => r.Id == id, ct);

    /// <inheritdoc />
    public async Task<List<PromotionRuleDefinitionAggregate>> GetEnabledAsync(CancellationToken ct = default)
        => await _context.PromotionRuleDefinitions
            .Where(r => r.Enabled)
            .OrderBy(r => r.Priority)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<PromotionRuleDefinitionAggregate?> GetByRuleTypeAsync(
        string ruleType,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(ruleType);
        return await _context.PromotionRuleDefinitions
            .Where(r => r.RuleType == ruleType && r.Enabled)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc />
    public async Task<List<PromotionRuleDefinitionAggregate>> GetByRuleTypesAsync(
        IEnumerable<string> ruleTypes,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(ruleTypes);
        var typeList = ruleTypes.Distinct().ToList();
        if (typeList.Count == 0)
        {
            return new List<PromotionRuleDefinitionAggregate>();
        }

        return await _context.PromotionRuleDefinitions
            .Where(r => r.Enabled && typeList.Contains(r.RuleType))
            .OrderBy(r => r.Priority)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task AddAsync(PromotionRuleDefinitionAggregate aggregate, CancellationToken ct = default)
        => await _context.PromotionRuleDefinitions.AddAsync(aggregate, ct);

    /// <inheritdoc />
    public Task UpdateAsync(PromotionRuleDefinitionAggregate aggregate, CancellationToken ct = default)
    {
        _context.PromotionRuleDefinitions.Update(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(PromotionRuleDefinitionAggregate aggregate, CancellationToken ct = default)
    {
        _context.PromotionRuleDefinitions.Remove(aggregate);
        return Task.CompletedTask;
    }
}
