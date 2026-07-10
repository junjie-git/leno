using Leno.UserAuth.Domain.Services;
using Microsoft.Extensions.Options;
using BCryptNet = BCrypt.Net.BCrypt;

namespace Leno.UserAuth.Infrastructure.Services;

/// <summary>
/// bcrypt 密码哈希与校验配置，对应 appsettings.json 中 <c>PasswordHash</c> 节。
/// </summary>
public sealed class PasswordHashOptions
{
    /// <summary>bcrypt 工作因子，决定哈希计算成本，须 ≥ 12。</summary>
    public int WorkFactor { get; set; } = 12;
}

/// <summary>
/// 基于 bcrypt 的密码哈希实现，cost ≥ 12。
/// 明文密码不落库不落日志，校验失败统一返回 false，避免泄露哈希内部状态。
/// </summary>
public sealed class BcryptPasswordHasher : IPasswordHasher
{
    private const int MinWorkFactor = 12;
    private readonly int _workFactor;

    public BcryptPasswordHasher(IOptions<PasswordHashOptions> options)
    {
        var configured = options?.Value?.WorkFactor ?? MinWorkFactor;
        _workFactor = configured < MinWorkFactor ? MinWorkFactor : configured;
    }

    /// <inheritdoc />
    public string Hash(string plainPassword)
    {
        if (string.IsNullOrEmpty(plainPassword))
        {
            throw new ArgumentException("明文密码不可为空", nameof(plainPassword));
        }

        return BCryptNet.HashPassword(plainPassword, _workFactor);
    }

    /// <inheritdoc />
    public bool Verify(string plainPassword, string hash)
    {
        if (string.IsNullOrEmpty(plainPassword) || string.IsNullOrEmpty(hash))
        {
            return false;
        }

        try
        {
            return BCryptNet.Verify(plainPassword, hash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            return false;
        }
    }
}
