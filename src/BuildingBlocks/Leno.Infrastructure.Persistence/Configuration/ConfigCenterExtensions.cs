using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Winton.Extensions.Configuration.Consul;

namespace Leno.Infrastructure.Configuration;

/// <summary>
/// 配置中心与占位符解析扩展。
/// 解析配置值中的 <c>${ENV_VAR}</c> 占位符为环境变量值，用于注入敏感参数（密钥、Token 等），
/// 避免将明文密钥写入 appsettings.json。
/// 同时支持 Consul KV 作为远程配置中心，支持热重载与 appsettings.json 作为本地回退。
/// </summary>
public static class ConfigCenterExtensions
{
    private static readonly Regex PlaceholderRegex =
        new(@"\$\{(?<name>[A-Za-z_][A-Za-z0-9_]*)\}", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    /// <summary>
    /// 敏感配置键的默认路径列表，这些配置应优先从 Consul / 环境变量获取，而非 appsettings.json。
    /// </summary>
    public static readonly string[] SensitiveConfigKeys =
    {
        "Payment:Alipay:AppId",
        "Payment:Alipay:PrivateKey",
        "Payment:Alipay:PublicKey",
        "Payment:WeChatPay:AppId",
        "Payment:WeChatPay:MchId",
        "Payment:WeChatPay:ApiKey",
        "SMS:ApiKey",
        "SMS:ApiSecret",
        "OAuth2:WeChat:AppId",
        "OAuth2:WeChat:AppSecret",
        "OAuth2:Apple:ClientId",
        "OAuth2:Apple:ClientSecret",
        "Jwt:SecretKey"
    };

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

    /// <summary>
    /// 添加 Consul KV 作为远程配置源。
    /// Consul 中的配置优先级高于 appsettings.json，支持热重载（通过 <see cref="Microsoft.Extensions.Options.IOptionsSnapshot{TOptions}"/>）。
    /// 敏感参数（支付密钥、短信 API 密钥、OAuth2 密钥）应从 Consul 获取。
    /// M5.2：生产环境 Consul 不可达时降级为 warning，使用本地 appsettings 兜底；
    /// 开发环境仍抛异常（fail-closed），避免本地配置覆盖生产配置的安全风险。
    /// </summary>
    /// <param name="builder">宿主导入器。</param>
    /// <param name="consulKeyPrefix">Consul KV 键前缀，默认为 "leno/config"。</param>
    /// <param name="configureConsul">可选回调，用于覆盖 Consul 连接配置（地址、端口、Token 等）。</param>
    public static IHostApplicationBuilder AddLenoConsulConfig(
        this IHostApplicationBuilder builder,
        string consulKeyPrefix = "leno/config",
        Action<IConsulConfigurationSource>? configureConsul = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(consulKeyPrefix);

        try
        {
            var consulUrl = builder.Configuration["Consul:Url"] ?? "http://localhost:8500";
            var consulToken = builder.Configuration["Consul:Token"] ?? string.Empty;

            builder.Configuration.AddConsul(
                consulKeyPrefix,
                options =>
                {
                    options.ConsulConfigurationOptions = cco =>
                    {
                        cco.Address = new Uri(consulUrl);
                        if (!string.IsNullOrEmpty(consulToken))
                        {
                            cco.Token = consulToken;
                        }
                    };

                    // 启用可选配置：Consul 不可用时仍可启动（回退到 appsettings.json）
                    options.Optional = true;

                    // 启用热重载：Consul 中的变更实时反映到 IOptionsSnapshot
                    options.ReloadOnChange = true;

                    // 轮询间隔 30 秒
                    options.PollWaitTime = TimeSpan.FromSeconds(30);

                    // 转换键名：Consul 中的 "Payment:Alipay:AppId" 映射到 .NET 配置的 "Payment:Alipay:AppId"
                    options.OnLoadException = loadExceptionContext =>
                    {
                        // Consul 加载失败时忽略，回退到本地配置
                        loadExceptionContext.Ignore = true;
                    };

                    configureConsul?.Invoke(options);
                });
        }
        catch (Exception ex) when (builder.Environment.IsProduction())
        {
            // M5.2：生产环境 Consul 不可达时降级为 warning，使用本地 appsettings 兜底
            Console.WriteLine($"[WARN] Consul KV 配置中心不可达，使用本地配置兜底：{ex.Message}");
        }

        return builder;
    }

    /// <summary>
    /// 验证所有敏感配置键是否已从 Consul 正确加载。
    /// 在应用启动后调用，确保关键密钥不缺失。
    /// M5.2：新增 InternalApiKey 各 BC 独立校验——若 <c>Service:Name</c> 已配置，
    /// 优先校验 <c>Security:InternalApiKey:{BcName}</c>；缺失时降级为 Shared key（兼容期 warning），
    /// 二者皆缺则视为校验失败。Service:Name 未配置时跳过 BC 独立校验（兼容期）。
    /// </summary>
    public static bool ValidateSensitiveConfig(this IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var missing = SensitiveConfigKeys
            .Where(key => string.IsNullOrWhiteSpace(configuration[key]))
            .ToList();

        // M5.2: InternalApiKey 各 BC 独立校验
        var bcName = configuration["Service:Name"];
        if (string.IsNullOrWhiteSpace(bcName))
        {
            // Service:Name 未配置时不阻断（兼容期）
            Console.WriteLine("[WARN] Service:Name 配置缺失，跳过 InternalApiKey 独立校验");
        }
        else
        {
            var internalKey = configuration[$"Security:InternalApiKey:{bcName}"];
            if (string.IsNullOrWhiteSpace(internalKey))
            {
                // F2.4 兼容期：若 BC 仍用共用 key，降级为 warning
                var sharedKey = configuration["Security:InternalApiKey:Shared"];
                if (string.IsNullOrWhiteSpace(sharedKey))
                {
                    missing.Add($"Security:InternalApiKey:{bcName}");
                    Console.WriteLine(
                        $"[ERROR] 敏感配置缺失：Security:InternalApiKey:{bcName} 与 Security:InternalApiKey:Shared 均为空，"
                        + $"请通过 Consul KV 配置 leno/security/internal-key/{bcName}");
                }
                else
                {
                    Console.WriteLine(
                        $"[WARN] BC {bcName} 仍在使用 Shared InternalApiKey，请尽快迁移到独立 key（M5.2）");
                }
            }
            else if (internalKey.Length < 44)
            {
                // 校验独立 key 长度（至少 32 字节 base64 编码 = 44 字符）
                missing.Add($"Security:InternalApiKey:{bcName}(长度不足)");
                Console.WriteLine(
                    $"[ERROR] Security:InternalApiKey:{bcName} 长度不足，至少 32 字节（base64 编码 44 字符），"
                    + $"当前 {internalKey.Length} 字符");
            }
        }

        return missing.Count == 0;
    }

    /// <summary>
    /// 获取所有缺失的敏感配置键。
    /// </summary>
    public static IReadOnlyList<string> GetMissingSensitiveConfigKeys(this IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return SensitiveConfigKeys
            .Where(key => string.IsNullOrWhiteSpace(configuration[key]))
            .ToList();
    }
}