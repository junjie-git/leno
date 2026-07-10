namespace Leno.Order.Infrastructure.Services;

/// <summary>
/// 第三方物流轨迹查询 API 配置项。
/// </summary>
public sealed class LogisticsApiOptions
{
    public const string SectionName = "LogisticsApi";

    /// <summary>物流轨迹查询 API 地址，为空时使用默认快递鸟地址。</summary>
    public string ApiUrl { get; set; } = string.Empty;

    /// <summary>物流 API 调用密钥，为空时不发送鉴权头。</summary>
    public string AppKey { get; set; } = string.Empty;
}
