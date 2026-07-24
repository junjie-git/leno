using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Leno.Infrastructure.Configuration;

/// <summary>
/// Consul 配置 Schema 版本校验器（阶段四 4.6 步骤6）。
/// 校验配置 JSON 中的 <c>schemaVersion</c> 字段，版本不匹配或哈希不一致时拒绝应用并触发告警。
/// </summary>
public sealed class ConsulConfigSchemaValidator
{
    private readonly ILogger<ConsulConfigSchemaValidator> _logger;

    public ConsulConfigSchemaValidator(ILogger<ConsulConfigSchemaValidator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 校验配置 JSON 的 schemaVersion 与期望版本是否匹配。
    /// </summary>
    /// <param name="configJson">Consul KV 中的配置 JSON 字符串。</param>
    /// <param name="expectedVersion">期望的 Schema 版本号。</param>
    /// <returns>校验结果。</returns>
    public SchemaValidationResult Validate(string configJson, int expectedVersion)
    {
        if (string.IsNullOrWhiteSpace(configJson))
        {
            _logger.LogWarning("配置 JSON 为空，校验失败");
            return SchemaValidationResult.Fail("配置 JSON 为空");
        }

        int? actualVersion = null;
        try
        {
            using var doc = JsonDocument.Parse(configJson);
            if (doc.RootElement.TryGetProperty("schemaVersion", out var versionElement))
            {
                if (versionElement.ValueKind == JsonValueKind.Number && versionElement.TryGetInt32(out var v))
                {
                    actualVersion = v;
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "配置 JSON 解析失败");
            return SchemaValidationResult.Fail($"配置 JSON 解析失败: {ex.Message}");
        }

        if (!actualVersion.HasValue)
        {
            _logger.LogWarning("配置 JSON 缺少 schemaVersion 字段");
            return SchemaValidationResult.Fail("配置 JSON 缺少 schemaVersion 字段");
        }

        if (actualVersion.Value != expectedVersion)
        {
            _logger.LogWarning("Schema 版本不匹配：期望 {Expected}，实际 {Actual}", expectedVersion, actualVersion.Value);
            return SchemaValidationResult.Fail($"Schema 版本不匹配：期望 {expectedVersion}，实际 {actualVersion.Value}");
        }

        _logger.LogInformation("Schema 版本校验通过：v{Version}", expectedVersion);
        return SchemaValidationResult.Ok(actualVersion.Value);
    }

    /// <summary>
    /// 校验配置内容的哈希是否与已记录的 Schema 版本一致（检测配置是否被篡改）。
    /// </summary>
    /// <param name="configJson">当前配置 JSON 字符串。</param>
    /// <param name="recordedVersion">已记录的 Schema 版本快照。</param>
    /// <returns>校验结果。</returns>
    public SchemaValidationResult ValidateHash(string configJson, ConsulSchemaVersion recordedVersion)
    {
        if (recordedVersion is null)
        {
            return SchemaValidationResult.Fail("已记录的 Schema 版本为 null");
        }

        var currentHash = ConsulSchemaVersion.ComputeSchemaHash(configJson);
        if (!string.Equals(currentHash, recordedVersion.SchemaHash, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("配置哈希不匹配：期望 {Expected}，实际 {Actual}（版本 v{Version}）",
                recordedVersion.SchemaHash, currentHash, recordedVersion.Version);
            return SchemaValidationResult.Fail($"配置哈希不匹配（版本 v{recordedVersion.Version}）");
        }

        return SchemaValidationResult.Ok(recordedVersion.Version);
    }
}

/// <summary>Schema 校验结果。</summary>
public sealed class SchemaValidationResult
{
    public bool IsValid { get; init; }

    public int Version { get; init; }

    public string ErrorMessage { get; init; } = string.Empty;

    public static SchemaValidationResult Ok(int version) => new() { IsValid = true, Version = version };

    public static SchemaValidationResult Fail(string error) => new() { IsValid = false, ErrorMessage = error };
}
