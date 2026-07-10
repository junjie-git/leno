using Leno.Order.Application.DTOs;
using Microsoft.Extensions.Logging;

namespace Leno.Order.Application.Services;

/// <summary>
/// 物流轨迹查询服务接口，封装第三方物流轨迹查询。
/// </summary>
public interface ILogisticsTrackingService
{
    /// <summary>
    /// 查询物流单号对应的物流轨迹。
    /// </summary>
    /// <param name="logisticsNo">物流单号。</param>
    Task<LogisticsTrackingDto> GetTrackingAsync(string logisticsNo, CancellationToken ct = default);
}

/// <summary>
/// 物流轨迹查询服务实现，未对接真实物流 API 前返回占位轨迹。
/// </summary>
public sealed class LogisticsTrackingService : ILogisticsTrackingService
{
    private readonly ILogger<LogisticsTrackingService> _logger;

    public LogisticsTrackingService(ILogger<LogisticsTrackingService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<LogisticsTrackingDto> GetTrackingAsync(string logisticsNo, CancellationToken ct = default)
    {
        _logger.LogInformation("查询物流轨迹：{LogisticsNo}", logisticsNo);

        // 未对接真实物流 API，返回占位轨迹节点
        var dto = new LogisticsTrackingDto
        {
            LogisticsNo = logisticsNo,
            Nodes = new List<LogisticsTrackingNode>
            {
                new()
                {
                    Description = "物流信息暂未更新",
                    OccurredAt = DateTime.UtcNow,
                    Location = string.Empty
                }
            }
        };
        return Task.FromResult(dto);
    }
}
