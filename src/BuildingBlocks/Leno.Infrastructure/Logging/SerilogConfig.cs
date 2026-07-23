using Serilog;
using Serilog.Core;

namespace Leno.Infrastructure.Logging;

/// <summary>
/// Serilog 配置入口，提供统一的结构化日志默认配置（JSON 输出、应用名、环境、TraceId 贯穿）。
/// </summary>
public static class SerilogConfig
{
    /// <summary>
    /// 在给定 <see cref="LoggerConfiguration"/> 上应用默认富化与控制台 JSON 输出。
    /// 调用方可继续链式追加文件 Sink 等。
    /// </summary>
    public static LoggerConfiguration ConfigureDefaults(
        LoggerConfiguration loggerConfig,
        string applicationName,
        string environmentName)
    {
        ArgumentNullException.ThrowIfNull(loggerConfig);
        return loggerConfig
            .Enrich.WithProperty("Application", applicationName)
            .Enrich.WithProperty("Environment", environmentName)
            .Enrich.FromLogContext()
            .Enrich.With<TraceIdEnricher>()
            .WriteTo.Console(new Serilog.Formatting.Json.JsonFormatter());
    }
}
