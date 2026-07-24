namespace Leno.Infrastructure.Abstractions.Localization;

/// <summary>
/// 本地化配置选项（国际化预留扩展位）。
/// </summary>
public sealed class LocalizationOptions
{
    /// <summary>支持的文化列表，默认 ["en-US", "zh-CN"]。</summary>
    public string[] SupportedCultures { get; set; } = { "en-US", "zh-CN" };

    /// <summary>默认文化（当前阶段为 zh-CN，DG-8 通过后按业务需求调整）。</summary>
    public string DefaultCulture { get; set; } = "zh-CN";
}
