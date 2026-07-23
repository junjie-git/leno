namespace Leno.Infrastructure.Security;

/// <summary>
/// Pepper 提供者抽象（3.10 安全技术栈升级）。
/// 解析优先级：KMS 解包 &gt; 环境变量 <c>PASSWORD_PEPPER</c> &gt; <see cref="PasswordHashOptions.Pepper"/>。
/// </summary>
public interface IPepperProvider
{
    /// <summary>获取当前生效的 pepper 值。结果在实例生命周期内缓存。</summary>
    /// <returns>pepper 字符串，未配置时返回空字符串。</returns>
    string GetPepper();
}
