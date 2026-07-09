using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace Leno.Infrastructure.Configuration;

/// <summary>
/// 配置中心与占位符解析扩展。
/// 解析配置值中的 <c>${ENV_VAR}</c> 占位符为环境变量值，用于注入敏感参数（密钥、Token 等），
/// 避免将明文密钥写入 appsettings.json。
/// </summary>
public static class ConfigCenterExtensions
{
    private static readonly Regex PlaceholderRegex =
        new(@"\$\{(?<name>[A-Za-z_][A-Za-z0-9_]*)\}", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    /// <summary>
    /// 解析字符串中的 <c>${ENV_VAR}</c> 占位符。
    /// 若环境变量存在则替换；不存在则保留原占位符文本，便于排查缺失配置。
    /// </summary>
    public static string ResolvePlaceholders(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value ?? string.Empty;
        }

        if (value.IndexOf('$') < 0)
        {
            return value;
        }

        return PlaceholderRegex.Replace(value, match =>
        {
            var name = match.Groups["name"].Value;
            return Environment.GetEnvironmentVariable(name) ?? match.Value;
        });
    }

    /// <summary>
    /// 遍历 <see cref="IConfiguration"/> 所有键值，返回占位符解析后的字典。
    /// </summary>
    public static IReadOnlyDictionary<string, string?> ResolveConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var resolved = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in configuration.AsEnumerable())
        {
            resolved[key] = ResolvePlaceholders(value);
        }

        return resolved;
    }

    /// <summary>
    /// 从 <see cref="IConfiguration"/> 读取指定键并解析占位符。
    /// </summary>
    public static string? GetResolvedValue(this IConfiguration configuration, string key)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return ResolvePlaceholders(configuration[key]);
    }

    /// <summary>
    /// 从 <see cref="IConfiguration"/> 读取指定键的连接字符串并解析占位符。
    /// </summary>
    public static string? GetResolvedConnectionString(this IConfiguration configuration, string name)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return ResolvePlaceholders(configuration.GetConnectionString(name));
    }
}
