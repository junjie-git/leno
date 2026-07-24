using Leno.ApiGateway.Bff.Models;

namespace Leno.ApiGateway.Bff.Dag;

/// <summary>
/// DAG 执行结果：包含已完成节点的结果字典与失败明细。
/// <para>
/// 与 <see cref="BffResponse{T}"/> 语义一致：
/// <list type="bullet">
///   <item>全部成功：Success=true、Partial=false、Errors=空</item>
///   <item>部分失败：Success=false、Partial=true、Results 含已成功节点、Errors 含失败明细</item>
///   <item>全部失败：Success=false、Partial=false、Errors 含全部失败明细</item>
/// </list>
/// </para>
/// </summary>
public sealed class AggregateResult
{
    /// <summary>全部节点均成功时为 true。</summary>
    public bool Success { get; init; }

    /// <summary>部分节点成功、部分失败时为 true。</summary>
    public bool Partial { get; init; }

    /// <summary>已完成节点的结果字典（以节点名为键）。</summary>
    public IReadOnlyDictionary<string, object?> Results { get; init; } = new Dictionary<string, object?>(StringComparer.Ordinal);

    /// <summary>失败节点的错误明细。全部成功时为空数组。</summary>
    public IReadOnlyList<BffError> Errors { get; init; } = Array.Empty<BffError>();

    /// <summary>
    /// 获取指定节点的结果并尝试转换为 <typeparamref name="T"/>。
    /// </summary>
    /// <typeparam name="T">目标类型。</typeparam>
    /// <param name="name">节点名。</param>
    /// <returns>转换后的结果；节点未完成或类型不匹配时返回 default。</returns>
    public T? GetResult<T>(string name)
    {
        if (Results.TryGetValue(name, out var value) && value is T typed)
        {
            return typed;
        }
        return default;
    }
}
