namespace Leno.Infrastructure.Abstractions.Localization;

/// <summary>
/// 本地化器工厂抽象（国际化预留扩展位）。
/// 按 <c>baseName</c>（如 "ErrorMessages"）创建对应的 <see cref="IStringLocalizer"/>。
/// </summary>
public interface IStringLocalizerFactory
{
    /// <summary>
    /// 创建指定资源基名的本地化器。
    /// </summary>
    /// <param name="baseName">资源基名（如 "ErrorMessages"），对应 <c>ErrorMessages.resx</c>。</param>
    IStringLocalizer Create(string baseName);
}
