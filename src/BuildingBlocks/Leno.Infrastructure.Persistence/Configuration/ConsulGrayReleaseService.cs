using Microsoft.Extensions.Logging;

namespace Leno.Infrastructure.Configuration;

/// <summary>
/// Consul 配置灰度发布服务（阶段四 4.6 步骤6）。
/// 按实例 ID 哈希切流，支持 0%→25%→50%→100% 渐进式灰度发布。
/// 通过 Consul KV <c>leno/config/{env}/{service}/gray-percent</c> 控制切流比例。
/// </summary>
public sealed class ConsulGrayReleaseService
{
    /// <summary>灰度比例 key 后缀。</summary>
    public const string GrayPercentKeySuffix = "/gray-percent";

    /// <summary>历史版本 key 后缀。</summary>
    public const string HistoryKeySuffix = "/history/";

    private readonly ILogger<ConsulGrayReleaseService> _logger;

    public ConsulGrayReleaseService(ILogger<ConsulGrayReleaseService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 判断指定实例是否应应用新配置（按实例 ID 哈希切流）。
    /// 灰度比例 0% 全拒绝，100% 全通过，中间值按哈希取模切流。
    /// </summary>
    /// <param name="instanceId">实例唯一标识（通常为机器名+进程ID或容器ID）。</param>
    /// <param name="grayPercent">灰度比例（0-100）。</param>
    /// <returns>true 表示该实例应应用新配置；false 表示仍使用旧配置。</returns>
    public bool ShouldApplyConfig(string instanceId, int grayPercent)
    {
        if (string.IsNullOrEmpty(instanceId))
        {
            _logger.LogWarning("实例 ID 为空，灰度切流默认不应用新配置");
            return false;
        }

        if (grayPercent >= 100)
        {
            _logger.LogDebug("灰度比例 {Percent}%，全量应用新配置", grayPercent);
            return true;
        }

        if (grayPercent <= 0)
        {
            _logger.LogDebug("灰度比例 {Percent}%，全量拒绝新配置", grayPercent);
            return false;
        }

        // 使用稳定哈希（StringComparer.OrdinalIgnoreCase 的 GetHashCode 不跨进程稳定，
        // 改用 SHA256 取前 4 字节确保跨实例一致性）
        var hash = ComputeStableHash(instanceId);
        var inGray = (hash % 100) < (uint)grayPercent;
        _logger.LogDebug("实例 {Instance} 灰度判定：哈希 {Hash}，比例 {Percent}%，应用 {Apply}",
            instanceId, hash, grayPercent, inGray);
        return inGray;
    }

    /// <summary>
    /// 解析灰度比例配置值（支持 "25"、"25%"、"0.25" 三种格式）。
    /// </summary>
    /// <param name="rawValue">Consul KV 中的原始值。</param>
    /// <returns>灰度百分比（0-100），解析失败返回 0。</returns>
    public int ParseGrayPercent(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return 0;
        }

        var trimmed = rawValue.Trim().TrimEnd('%');
        if (int.TryParse(trimmed, out var percent))
        {
            return Math.Clamp(percent, 0, 100);
        }

        if (double.TryParse(trimmed, out var ratio))
        {
            // 0.25 → 25%
            return Math.Clamp((int)Math.Round(ratio * 100), 0, 100);
        }

        _logger.LogWarning("灰度比例配置值 {Value} 无法解析，默认 0%", rawValue);
        return 0;
    }

    /// <summary>计算字符串的稳定哈希（跨进程一致，基于 SHA256 前 4 字节）。</summary>
    private static uint ComputeStableHash(string input)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input));
        // 取前 4 字节作为 uint
        return BitConverter.ToUInt32(bytes, 0);
    }
}
