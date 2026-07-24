using System.Text;
using System.Text.Json;
using Consul;
using Microsoft.Extensions.Logging;

namespace Leno.Infrastructure.Configuration;

/// <summary>
/// Consul 配置发布器（阶段四 4.6 步骤6）。
/// 发布配置时自动写入 schemaVersion + 历史版本快照，支持配置变更可追溯、可回滚。
/// Consul KV 路径约定：
/// <list type="bullet">
/// <item><c>leno/config/{env}/{service}</c>：当前配置</item>
/// <item><c>leno/config/{env}/{service}/schema-version</c>：当前 Schema 版本元数据</item>
/// <item><c>leno/config/{env}/{service}/history/{version}</c>：历史版本快照</item>
/// <item><c>leno/config/{env}/{service}/gray-percent</c>：灰度比例</item>
/// </list>
/// </summary>
public sealed class ConsulConfigPublisher
{
    private const string ConfigKeyPrefix = "leno/config/";
    private const string SchemaVersionKeySuffix = "/schema-version";
    private const string HistoryKeyPrefix = "/history/";
    private const string GrayPercentKeySuffix = "/gray-percent";

    private readonly IConsulClient _consul;
    private readonly ConsulConfigSchemaValidator _validator;
    private readonly ILogger<ConsulConfigPublisher> _logger;

    public ConsulConfigPublisher(
        IConsulClient consul,
        ConsulConfigSchemaValidator validator,
        ILogger<ConsulConfigPublisher> logger)
    {
        _consul = consul ?? throw new ArgumentNullException(nameof(consul));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 发布新版本配置：写入配置 JSON、记录 Schema 版本、归档历史版本。
    /// </summary>
    /// <param name="env">环境名（如 dev/staging/prod）。</param>
    /// <param name="service">服务名。</param>
    /// <param name="configJson">配置 JSON 字符串（需含 schemaVersion 字段）。</param>
    /// <param name="appliedBy">应用者标识。</param>
    /// <param name="grayPercent">灰度比例（0-100），100 表示全量发布。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>发布的 Schema 版本快照。</returns>
    public async Task<ConsulSchemaVersion> PublishAsync(
        string env,
        string service,
        string configJson,
        string appliedBy,
        int grayPercent = 100,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(env))
        {
            throw new ArgumentNullException(nameof(env));
        }
        if (string.IsNullOrEmpty(service))
        {
            throw new ArgumentNullException(nameof(service));
        }
        if (string.IsNullOrWhiteSpace(configJson))
        {
            throw new ArgumentException("配置 JSON 不能为空", nameof(configJson));
        }

        // 解析 schemaVersion
        var version = ExtractSchemaVersion(configJson);
        var baseKey = $"{ConfigKeyPrefix}{env}/{service}";

        // 创建版本快照
        var schemaVersion = ConsulSchemaVersion.Create(version, configJson, appliedBy);

        // 1. 写入当前配置
        await PutKvAsync(baseKey, configJson, ct).ConfigureAwait(false);
        _logger.LogInformation("配置已发布到 {Key}（v{Version}）", baseKey, version);

        // 2. 写入 Schema 版本元数据
        var versionJson = JsonSerializer.Serialize(schemaVersion, new JsonSerializerOptions
        {
            WriteIndented = false
        });
        await PutKvAsync(baseKey + SchemaVersionKeySuffix, versionJson, ct).ConfigureAwait(false);
        _logger.LogInformation("Schema 版本元数据已记录：{Version}", schemaVersion);

        // 3. 归档历史版本
        var historyKey = baseKey + HistoryKeyPrefix + version;
        await PutKvAsync(historyKey, configJson, ct).ConfigureAwait(false);
        _logger.LogInformation("历史版本已归档：{Key}", historyKey);

        // 4. 写入灰度比例
        await PutKvAsync(baseKey + GrayPercentKeySuffix, grayPercent.ToString(), ct).ConfigureAwait(false);
        _logger.LogInformation("灰度比例设置为 {Percent}%", grayPercent);

        return schemaVersion;
    }

    /// <summary>
    /// 回滚到指定历史版本。
    /// </summary>
    /// <param name="env">环境名。</param>
    /// <param name="service">服务名。</param>
    /// <param name="targetVersion">目标版本号。</param>
    /// <param name="appliedBy">操作者。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>回滚后的 Schema 版本快照；目标版本不存在返回 null。</returns>
    public async Task<ConsulSchemaVersion?> RollbackAsync(
        string env,
        string service,
        int targetVersion,
        string appliedBy,
        CancellationToken ct = default)
    {
        var baseKey = $"{ConfigKeyPrefix}{env}/{service}";
        var historyKey = baseKey + HistoryKeyPrefix + targetVersion;

        // 读取历史版本配置
        var historyResult = await _consul.KV.Get(historyKey, ct).ConfigureAwait(false);
        if (historyResult.Response is null)
        {
            _logger.LogWarning("历史版本 v{Version} 不存在，回滚失败", targetVersion);
            return null;
        }

        var configJson = Encoding.UTF8.GetString(historyResult.Response.Value);
        var schemaVersion = ConsulSchemaVersion.Create(targetVersion, configJson, appliedBy + "-rollback");

        // 写回当前配置
        await PutKvAsync(baseKey, configJson, ct).ConfigureAwait(false);

        // 更新 Schema 版本元数据
        var versionJson = JsonSerializer.Serialize(schemaVersion, new JsonSerializerOptions
        {
            WriteIndented = false
        });
        await PutKvAsync(baseKey + SchemaVersionKeySuffix, versionJson, ct).ConfigureAwait(false);

        // 灰度比例重置为 100%（全量回滚）
        await PutKvAsync(baseKey + GrayPercentKeySuffix, "100", ct).ConfigureAwait(false);

        _logger.LogWarning("配置已回滚到 v{Version}（操作者：{By}）", targetVersion, appliedBy);
        return schemaVersion;
    }

    /// <summary>
    /// 读取当前 Schema 版本元数据。
    /// </summary>
    public async Task<ConsulSchemaVersion?> GetCurrentSchemaVersionAsync(
        string env,
        string service,
        CancellationToken ct = default)
    {
        var key = $"{ConfigKeyPrefix}{env}/{service}" + SchemaVersionKeySuffix;
        var result = await _consul.KV.Get(key, ct).ConfigureAwait(false);
        if (result.Response is null)
        {
            return null;
        }

        var json = Encoding.UTF8.GetString(result.Response.Value);
        return JsonSerializer.Deserialize<ConsulSchemaVersion>(json);
    }

    /// <summary>提取配置 JSON 中的 schemaVersion 字段。</summary>
    private static int ExtractSchemaVersion(string configJson)
    {
        using var doc = JsonDocument.Parse(configJson);
        if (doc.RootElement.TryGetProperty("schemaVersion", out var el) &&
            el.ValueKind == JsonValueKind.Number &&
            el.TryGetInt32(out var v))
        {
            return v;
        }
        throw new InvalidOperationException("配置 JSON 缺少 schemaVersion 字段或字段不是整数");
    }

    /// <summary>写入 Consul KV。</summary>
    private async Task PutKvAsync(string key, string value, CancellationToken ct)
    {
        var kvPair = new KVPair(key)
        {
            Value = Encoding.UTF8.GetBytes(value)
        };
        await _consul.KV.Put(kvPair, ct).ConfigureAwait(false);
    }
}
