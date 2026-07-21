using Leno.Infrastructure.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Leno.Infrastructure.Tests.Auth;

/// <summary>
/// T22 + T23 验证：JwtTokenGenerator 密钥长度校验 + ClockSkew 缩短为 30s。
/// </summary>
public class JwtTokenGeneratorKeyAndClockSkewTests
{
    private static JwtOptions CreateOptions(string secretKey) => new()
    {
        Issuer = "leno-test",
        Audience = "leno-test-audience",
        SecretKey = secretKey,
        AccessTokenExpiryMinutes = 60,
        RefreshTokenExpiryDays = 7
    };

    private static JwtTokenGenerator CreateGenerator(string secretKey)
        => new(Options.Create(CreateOptions(secretKey)));

    [Fact]
    public void Constructor_ShortSecretKey_ThrowsInvalidOperationException()
    {
        // Arrange — 16 字节密钥（128 位），不满足 HS256 的 256 位要求
        var shortKey = new string('a', 16);

        // Act
        var act = () => CreateGenerator(shortKey);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*32 字节*");
    }

    [Fact]
    public void Constructor_EmptySecretKey_ThrowsInvalidOperationException()
    {
        var act = () => CreateGenerator(string.Empty);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Constructor_NullSecretKey_ThrowsInvalidOperationException()
    {
        var act = () => CreateGenerator(null!);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Constructor_Exactly32ByteKey_Succeeds()
    {
        // Arrange — 正好 32 字节（256 位），HS256 最低要求
        var validKey = new string('a', 32);

        // Act
        var generator = CreateGenerator(validKey);

        // Assert
        generator.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_LongerThan32ByteKey_Succeeds()
    {
        // Arrange — 64 字节（512 位），超过最低要求
        var longKey = new string('a', 64);

        // Act
        var generator = CreateGenerator(longKey);

        // Assert
        generator.Should().NotBeNull();
    }

    [Fact]
    public void BuildValidationParameters_ClockSkew_Is30Seconds()
    {
        // Arrange
        var generator = CreateGenerator(new string('a', 32));

        // Act
        var parameters = generator.BuildValidationParameters();

        // Assert — T23: ClockSkew 从 1 分钟缩短为 30 秒
        parameters.ClockSkew.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void BuildValidationParameters_ClockSkew_IsNotOneMinute()
    {
        // Arrange
        var generator = CreateGenerator(new string('a', 32));

        // Act
        var parameters = generator.BuildValidationParameters();

        // Assert — 确保不是旧的 1 分钟值
        parameters.ClockSkew.Should().NotBe(TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void GenerateAccessToken_ValidKey_GeneratesToken()
    {
        // Arrange
        var generator = CreateGenerator(new string('a', 32));
        var userId = Guid.NewGuid();

        // Act
        var token = generator.GenerateAccessToken(userId, "customer", null);

        // Assert
        token.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ValidateTokenAsync_GeneratedToken_ReturnsValidPrincipal()
    {
        // Arrange
        var generator = CreateGenerator(new string('a', 32));
        var userId = Guid.NewGuid();
        var token = generator.GenerateAccessToken(userId, "customer", shopId: Guid.NewGuid());

        // Act
        var principal = await generator.ValidateTokenAsync(token);

        // Assert
        principal.Should().NotBeNull();
        JwtTokenGenerator.GetUserId(principal).Should().Be(userId);
    }
}
