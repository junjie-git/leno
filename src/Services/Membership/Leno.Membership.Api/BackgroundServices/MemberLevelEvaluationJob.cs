using Leno.Membership.Domain.Repositories;
using Leno.Membership.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Leno.Membership.Api.BackgroundServices;

/// <summary>
/// 会员成长值等级评估定时任务，每日扫描所有活跃会员的成长值，评估 V0-V4 等级并发布变更事件。
/// 批次大小：500。
/// 异常后采用指数退避（1min/5min/30min/1h），正常执行后恢复 24h 扫描间隔。
/// </summary>
public sealed class MemberLevelEvaluationJob : BackgroundService
{
    private const int BatchSize = 500;
    private static readonly TimeSpan ScanInterval = TimeSpan.FromDays(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MemberLevelEvaluationJob> _logger;

    public MemberLevelEvaluationJob(
        IServiceScopeFactory scopeFactory,
        ILogger<MemberLevelEvaluationJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MemberLevelEvaluationJob 启动，扫描间隔 {Interval}", ScanInterval);

        var failureCount = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await EvaluateAllMembersAsync(stoppingToken);
                failureCount = 0;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                failureCount++;
                _logger.LogError(ex,
                    "会员成长值等级评估异常，连续失败 {FailureCount} 次，将在 {Delay} 后重试",
                    failureCount, ComputeBackoffDelay(failureCount));
            }

            var delay = failureCount > 0 ? ComputeBackoffDelay(failureCount) : ScanInterval;
            await Task.Delay(delay, stoppingToken);
        }
    }

    /// <summary>
    /// 根据连续失败次数计算指数退避延迟。
    /// 第 1 次：1 分钟；第 2 次：5 分钟；第 3 次：30 分钟；第 4 次及以上：1 小时。
    /// </summary>
    private static TimeSpan ComputeBackoffDelay(int failureCount) => failureCount switch
    {
        1 => TimeSpan.FromMinutes(1),
        2 => TimeSpan.FromMinutes(5),
        3 => TimeSpan.FromMinutes(30),
        _ => TimeSpan.FromHours(1)
    };

    private async Task EvaluateAllMembersAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var memberRepository = scope.ServiceProvider.GetRequiredService<IMemberRepository>();
        var memberLevelDefinitionRepository = scope.ServiceProvider.GetRequiredService<IMemberLevelDefinitionRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var definitions = await memberLevelDefinitionRepository.GetAllAsync(ct);

        if (definitions.Count == 0)
        {
            _logger.LogWarning("未配置成长值等级定义，跳过评估");
            return;
        }

        // 仅启用等级定义参与评估，停用等级不参与（运营停用后已有会员等级不受影响，但新评估不再匹配停用等级）
        var levels = definitions
            .Where(d => d.Status == LevelDefinitionStatus.Enabled)
            .Select(d => d.ToValueObject())
            .ToList();

        var totalEvaluated = 0;
        var totalChanged = 0;
        var skip = 0;

        while (!ct.IsCancellationRequested)
        {
            var batch = await memberRepository.GetAllActiveAsync(skip, BatchSize, ct);

            if (batch.Count == 0)
            {
                break;
            }

            var changedInBatch = 0;
            foreach (var member in batch)
            {
                var oldLevel = member.CurrentGrowthLevel;
                member.EvaluateGrowthLevel(levels);
                await memberRepository.UpdateAsync(member, ct);

                if (member.CurrentGrowthLevel != oldLevel)
                {
                    changedInBatch++;
                }
            }

            await unitOfWork.SaveEntitiesAsync(ct);
            totalEvaluated += batch.Count;
            totalChanged += changedInBatch;
            skip += BatchSize;

            _logger.LogDebug(
                "已评估一批会员，本批 {Count} 人，等级变更 {Changed} 人，累计评估 {TotalEvaluated}，累计变更 {TotalChanged}",
                batch.Count, changedInBatch, totalEvaluated, totalChanged);
        }

        _logger.LogInformation(
            "会员成长值等级评估完成，共评估 {TotalEvaluated} 人，等级变更 {TotalChanged} 人",
            totalEvaluated, totalChanged);
    }
}
