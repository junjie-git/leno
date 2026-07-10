using Leno.Infrastructure.Auth;
using Leno.UserAuth.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace Leno.UserAuth.Infrastructure.Services;

/// <summary>
/// 访问令牌生成实现，包装共享内核 <see cref="JwtTokenGenerator"/>。
/// 应用层经 <see cref="ITokenService"/> 抽象签发令牌，不直接依赖 JWT 库。
/// </summary>
public sealed class TokenService : ITokenService
{
    private readonly JwtTokenGenerator _generator;
    private readonly JwtOptions _options;

    public TokenService(JwtTokenGenerator generator, IOptions<JwtOptions> options)
    {
        ArgumentNullException.ThrowIfNull(generator);
        _generator = generator;
        _options = options?.Value ?? throw new InvalidOperationException("JwtOptions 未配置");
    }

    /// <inheritdoc />
    public string GenerateAccessToken(Guid userId, string role, Guid? shopId = null)
        => _generator.GenerateAccessToken(userId, role, shopId);

    /// <inheritdoc />
    public int AccessTokenExpirySeconds => _options.AccessTokenExpiryMinutes * 60;
}
