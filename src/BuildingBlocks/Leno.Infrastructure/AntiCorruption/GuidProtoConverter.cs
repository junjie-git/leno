namespace Leno.Infrastructure.AntiCorruption;

/// <summary>
/// Guid 与 proto string 字段之间的统一转换工具。
/// 替代历史 POC 阶段的 (long)guid.GetHashCode() 不可逆映射（ADR-0006/0007）。
/// 所有 gRPC 服务端填充 string xxx_id_str 字段时应使用此工具。
/// 所有 gRPC 客户端读取 string xxx_id_str 字段时应使用此工具解析。
/// </summary>
public static class GuidProtoConverter
{
    /// <summary>
    /// 将 Guid 转换为 proto string 字段值（D 格式：xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx）。
    /// </summary>
    public static string ToString(Guid guid) => guid.ToString("D");

    /// <summary>
    /// 尝试将 proto string 字段值解析为 Guid。
    /// 解析失败返回 false 且 result 为 Guid.Empty。
    /// </summary>
    public static bool TryParse(string? value, out Guid result)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result = Guid.Empty;
            return false;
        }

        return Guid.TryParse(value, out result);
    }

    /// <summary>
    /// 将 proto string 字段值解析为 Guid，解析失败抛 <see cref="FormatException"/>。
    /// </summary>
    public static Guid Parse(string? value)
    {
        if (!TryParse(value, out var result))
        {
            throw new FormatException($"无效的 Guid 字符串: {value}");
        }
        return result;
    }
}
