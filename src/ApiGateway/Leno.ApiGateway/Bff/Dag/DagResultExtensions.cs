using System.Text.Json;

namespace Leno.ApiGateway.Bff.Dag;

/// <summary>
/// DAG 节点执行委托中从上游结果字典提取类型化值的辅助扩展。
/// <para>
/// 节点 Executor 接收 <see cref="IReadOnlyDictionary{TKey, TValue}"/>（string → object?），
/// 本扩展提供从 object? 安全提取 <see cref="JsonElement"/>? 的便捷方法。
/// </para>
/// </summary>
public static class DagResultExtensions
{
    /// <summary>
    /// 从结果字典中获取指定节点的 <see cref="JsonElement"/>? 值。
    /// </summary>
    /// <param name="ctx">已完成节点结果字典。</param>
    /// <param name="nodeName">上游节点名。</param>
    /// <returns>节点的 JsonElement 值；节点未完成或值非 JsonElement 时返回 null。</returns>
    public static JsonElement? GetJsonValue(this IReadOnlyDictionary<string, object?> ctx, string nodeName)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        if (ctx.TryGetValue(nodeName, out var value) && value is JsonElement je)
        {
            return je;
        }
        return null;
    }

    /// <summary>
    /// 从结果字典中获取指定节点的值并尝试转换为 <typeparamref name="T"/>。
    /// </summary>
    /// <typeparam name="T">目标类型。</typeparam>
    /// <param name="ctx">已完成节点结果字典。</param>
    /// <param name="nodeName">上游节点名。</param>
    /// <returns>转换后的值；未完成或类型不匹配时返回 default。</returns>
    public static T? GetValue<T>(this IReadOnlyDictionary<string, object?> ctx, string nodeName)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        if (ctx.TryGetValue(nodeName, out var value) && value is T typed)
        {
            return typed;
        }
        return default;
    }
}
