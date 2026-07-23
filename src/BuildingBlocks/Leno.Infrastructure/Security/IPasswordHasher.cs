namespace Leno.Infrastructure.Security;

/// <summary>
/// 统一密码哈希抽象（3.10 安全技术栈升级），支持 Argon2id 新签发与 bcrypt 旧哈希兼容校验。
/// <para>
/// 实现位于 <see cref="Argon2PasswordHasher"/>，对调用方屏蔽算法细节与 pepper 注入。
/// </para>
/// </summary>
public interface IPasswordHasher
{
    /// <summary>使用当前算法（Argon2id）对明文密码进行哈希，内部自动注入 pepper。</summary>
    /// <param name="password">明文密码。</param>
    /// <returns>PHC 格式字符串（$argon2id$v=19$m=...,t=...,p=...$salt$hash）。</returns>
    string HashPassword(string password);

    /// <summary>
    /// 校验明文密码与已存哈希是否匹配。
    /// 自动识别哈希算法（Argon2id / bcrypt），对 bcrypt 旧哈希走兼容校验路径。
    /// </summary>
    /// <param name="password">明文密码。</param>
    /// <param name="hash">已存哈希字符串。</param>
    /// <returns>匹配返回 true，否则 false。</returns>
    bool VerifyPassword(string password, string hash);

    /// <summary>检测已存哈希使用的算法，用于懒迁移判断。</summary>
    /// <param name="hash">已存哈希字符串。</param>
    /// <returns>算法枚举值。</returns>
    PasswordHashAlgorithm DetectAlgorithm(string hash);
}

/// <summary>
/// 密码哈希算法标识。
/// </summary>
public enum PasswordHashAlgorithm
{
    /// <summary>bcrypt（$2a$ / $2b$ / $2y$ 前缀），旧哈希兼容。</summary>
    Bcrypt,

    /// <summary>Argon2id（$argon2id$ 前缀），当前推荐算法。</summary>
    Argon2id
}
