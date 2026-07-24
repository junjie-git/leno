namespace Leno.Infrastructure.Abstractions.Localization;

/// <summary>
/// 字符串本地化抽象（国际化预留扩展位，DG-8 决策门通过后实际启用）。
/// <para>
/// 当前阶段默认注册 <c>NullStringLocalizer</c>（空实现，原样返回 key），
/// 不改变现有错误消息行为。业务方确认海外扩展计划后切换为
/// <c>ResourceManagerStringLocalizer</c> 按 <c>Accept-Language</c> 解析多语言资源。
/// </para>
/// </summary>
public interface IStringLocalizer
{
    /// <summary>
    /// 按 key 查询本地化字符串。未命中时返回 key 本身（保证调用方始终拿到非空字符串）。
    /// </summary>
    string this[string key] { get; }

    /// <summary>
    /// 按 key 查询本地化字符串并以 <paramref name="arguments"/> 格式化。
    /// 未命中时返回 <c>string.Format(key, arguments)</c>（保证调用方始终拿到非空字符串）。
    /// </summary>
    string this[string key, params object[] arguments] { get; }
}
