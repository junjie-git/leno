using BCryptNet = BCrypt.Net.BCrypt;

namespace Leno.Infrastructure.Security;

/// <summary>
/// bcrypt 旧哈希兼容校验器（3.10 安全技术栈升级）。
/// 仅校验不生成新 bcrypt 哈希，用于懒迁移期间验证历史用户密码。
/// </summary>
public sealed class BcryptPasswordVerifier
{
    /// <summary>
    /// 校验明文密码与 bcrypt 哈希是否匹配。
    /// </summary>
    /// <param name="password">明文密码（不含 pepper，pepper 由 <see cref="Argon2PasswordHasher"/> 注入）。</param>
    /// <param name="hash">bcrypt 哈希字符串。</param>
    /// <returns>匹配返回 true，否则 false。解析异常统一返回 false。</returns>
    public bool Verify(string password, string hash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hash))
        {
            return false;
        }

        try
        {
            return BCryptNet.Verify(password, hash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            return false;
        }
    }
}
