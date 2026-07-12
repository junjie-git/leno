using Leno.PointsMembership.Domain.Aggregates;
using Leno.PointsMembership.Domain.Repositories;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;

namespace Leno.PointsMembership.Api.BackgroundServices;

/// <summary>
/// 会员成长值等级评估定时任务，每日扫描所有活跃会员的成长值，评估 V0-V4 等级并发布变更事件。
/// 批次大小：500。
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

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await EvaluateAllMembersAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "会员成长值等级评估异常");
            }

            await Task.Delay(ScanInterval, stoppingToken);
        }
    }

    private async Task EvaluateAllMembersAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var memberRepository = scope.ServiceProvider.GetRequiredService<IMemberRepository>();
        var memberLevelRepository = scope.ServiceProvider.GetRequiredService<IMemberLevelRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var levels = await memberLevelRepository.GetAllAsync(ct);

        if (levels.Count == 0)
        {
            _logger.LogWarning("未配置成长值等级定义，跳过评估");
            return;
        }

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