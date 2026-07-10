namespace Leno.UserAuth.Domain.Services;

/// <summary>
/// 密码哈希领域服务抽象，定义密码哈希与校验契约。
/// 实现位于基础设施层（bcrypt，cost ≥ 12），明文不落库不落日志。
/// </summary>
public interface IPasswordHasher
{
    /// <summary>将明文密码哈希为不可逆字符串。</summary>
    string Hash(string plainPassword);

    /// <summary>校验明文密码与已存哈希是否匹配。</summary>
    bool Verify(string plainPassword, string hash);
}
