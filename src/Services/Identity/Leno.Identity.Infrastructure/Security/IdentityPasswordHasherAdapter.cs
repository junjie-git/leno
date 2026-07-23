using Leno.Identity.Domain.Services;

namespace Leno.Identity.Infrastructure.Security;

/// <summary>
/// 密码哈希适配器（3.10 安全技术栈升级）。
/// <para>
/// 桥接 Identity BC 领域端口 <see cref="IPasswordHasher"/>（Hash/Verify）
/// 与共享基础设施 <see cref="Leno.Infrastructure.Security.IPasswordHasher"/>（HashPassword/VerifyPassword/DetectAlgorithm）。
/// 领域层依赖 Domain.IPasswordHasher 端口，实际算法实现由 Infrastructure.Security.Argon2PasswordHasher 提供。
/// </para>
/// </summary>
public sealed class IdentityPasswordHasherAdapter : IPasswordHasher
{
    private readonly Leno.Infrastructure.Security.IPasswordHasher _inner;

    public IdentityPasswordHasherAdapter(Leno.Infrastructure.Security.IPasswordHasher inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    /// <inheritdoc />
    public string Hash(string plainPassword)
    {
        return _inner.HashPassword(plainPassword);
    }

    /// <inheritdoc />
    public bool Verify(string plainPassword, string hash)
    {
        return _inner.VerifyPassword(plainPassword, hash);
    }
}
