using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Leno.Infrastructure.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moq;

namespace Leno.Infrastructure.Tests.Security;

/// <summary>
/// RsaJwtSigningService 单元测试（3.10 安全技术栈升级 / HS256 → RS256 过渡）。
/// 覆盖 HS256/RS256/Dual 三种签名模式的签名与验签，使用内存 RSA 密钥对。
/// </summary>
public class RsaJwtSigningServiceTests
{
    private const string TestIssuer = "leno-identity-test";
    private const string TestAudience = "leno-clients-test";
    private const string TestKeyId = "key-v1";
    private const string TestSubject = "user-123";
    private static readonly string ValidHs256Key = new string('x', 48);

    [Fact]
    public async Task SignAsync_In_Hs256_Mode_Should_Produce_Valid_Jwt()
    {
        var service = CreateService(signingMode: "Hs256", hs256Key: ValidHs256Key);

        var payload = CreateValidPayload();
        var token = await service.SignAsync(payload, CancellationToken.None);

        token.Should().NotBeNullOrEmpty();
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        jwt.Header.Alg.Should().Be("HS256");
    }

    [Fact]
    public async Task SignAsync_In_Rs256_Mode_Should_Produce_Valid_Jwt()
    {
        var (service, _) = CreateServiceWithRsa(signingMode: "Rs256");

        var payload = CreateValidPayload();
        var token = await service.SignAsync(payload, CancellationToken.None);

        token.Should().NotBeNullOrEmpty();
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        jwt.Header.Alg.Should().Be("RS256");
    }

    [Fact]
    public async Task SignAsync_In_Dual_Mode_Should_Produce_RS256_Signed_Jwt()
    {
        var (service, _) = CreateServiceWithRsa(signingMode: "Dual", hs256Key: ValidHs256Key);

        var payload = CreateValidPayload();
        var token = await service.SignAsync(payload, CancellationToken.None);

        token.Should().NotBeNullOrEmpty();
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        jwt.Header.Alg.Should().Be("RS256", "Dual 模式新令牌使用 RS256 签名");
    }

    [Fact]
    public async Task SignAsync_With_Null_Payload_Should_Throw()
    {
        var service = CreateService(signingMode: "Hs256", hs256Key: ValidHs256Key);

        var act = async () => await service.SignAsync(null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SignAsync_In_Hs256_Mode_With_Missing_Key_Should_Throw()
    {
        var service = CreateService(signingMode: "Hs256", hs256Key: string.Empty);

        var payload = CreateValidPayload();
        var act = async () => await service.SignAsync(payload, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Hs256SigningKey*");
    }

    [Fact]
    public async Task SignAsync_In_Hs256_Mode_With_Short_Key_Should_Throw()
    {
        var shortKey = new string('a', 16);
        var service = CreateService(signingMode: "Hs256", hs256Key: shortKey);

        var payload = CreateValidPayload();
        var act = async () => await service.SignAsync(payload, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*32 字节*");
    }

    [Fact]
    public async Task VerifyAsync_In_Hs256_Mode_Should_Validate_Correctly_Signed_Token()
    {
        var service = CreateService(signingMode: "Hs256", hs256Key: ValidHs256Key);
        var payload = CreateValidPayload();
        var token = await service.SignAsync(payload, CancellationToken.None);

        var result = await service.VerifyAsync(token, CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyAsync_In_Rs256_Mode_Should_Validate_Correctly_Signed_Token()
    {
        var (service, _) = CreateServiceWithRsa(signingMode: "Rs256");
        var payload = CreateValidPayload();
        var token = await service.SignAsync(payload, CancellationToken.None);

        var result = await service.VerifyAsync(token, CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyAsync_In_Dual_Mode_Should_Validate_RS256_Signed_Token()
    {
        var (service, _) = CreateServiceWithRsa(signingMode: "Dual", hs256Key: ValidHs256Key);
        var payload = CreateValidPayload();
        var token = await service.SignAsync(payload, CancellationToken.None);

        var result = await service.VerifyAsync(token, CancellationToken.None);

        result.Should().BeTrue("Dual 模式应能验签 RS256 签名的令牌");
    }

    [Fact]
    public async Task VerifyAsync_In_Dual_Mode_Should_Fallback_To_HS256_For_Legacy_Token()
    {
        var (service, _) = CreateServiceWithRsa(signingMode: "Dual", hs256Key: ValidHs256Key);

        // 先用 HS256 模式生成旧令牌
        var hs256Service = CreateService(signingMode: "Hs256", hs256Key: ValidHs256Key);
        var payload = CreateValidPayload();
        var legacyToken = await hs256Service.SignAsync(payload, CancellationToken.None);

        // Dual 模式应能验签 HS256 旧令牌
        var result = await service.VerifyAsync(legacyToken, CancellationToken.None);

        result.Should().BeTrue("Dual 模式应回退验签 HS256 旧令牌（过渡兼容）");
    }

    [Fact]
    public async Task VerifyAsync_Should_Reject_Tampered_Token()
    {
        var service = CreateService(signingMode: "Hs256", hs256Key: ValidHs256Key);
        var payload = CreateValidPayload();
        var token = await service.SignAsync(payload, CancellationToken.None);

        // 篡改 payload 部分（第二段）
        var parts = token.Split('.');
        var tamperedToken = $"{parts[0]}.{parts[1][..^4]}AAAA.{parts[2]}";

        var result = await service.VerifyAsync(tamperedToken, CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyAsync_With_Empty_Token_Should_Return_False()
    {
        var service = CreateService(signingMode: "Hs256", hs256Key: ValidHs256Key);

        var result = await service.VerifyAsync(string.Empty, CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyAsync_With_Null_Token_Should_Return_False()
    {
        var service = CreateService(signingMode: "Hs256", hs256Key: ValidHs256Key);

        var result = await service.VerifyAsync(null!, CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyAsync_With_Whitespace_Token_Should_Return_False()
    {
        var service = CreateService(signingMode: "Hs256", hs256Key: ValidHs256Key);

        var result = await service.VerifyAsync("   ", CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyAsync_With_Expired_Token_Should_Return_False()
    {
        var service = CreateService(signingMode: "Hs256", hs256Key: ValidHs256Key);

        // 创建已过期的 payload
        var now = DateTime.UtcNow;
        var payload = new JwtPayload(
            issuer: TestIssuer,
            audience: TestAudience,
            claims: new[] { new Claim(JwtRegisteredClaimNames.Sub, TestSubject), new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()) },
            notBefore: now.AddHours(-2),
            expires: now.AddHours(-1));

        var token = await service.SignAsync(payload, CancellationToken.None);

        var result = await service.VerifyAsync(token, CancellationToken.None);

        result.Should().BeFalse("过期令牌应验签失败");
    }

    [Fact]
    public async Task VerifyAsync_With_Wrong_Issuer_Should_Return_False()
    {
        var service = CreateService(signingMode: "Hs256", hs256Key: ValidHs256Key);

        var payload = new JwtPayload(
            issuer: "wrong-issuer",
            audience: TestAudience,
            claims: new[] { new Claim(JwtRegisteredClaimNames.Sub, TestSubject) },
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(30));

        var token = await service.SignAsync(payload, CancellationToken.None);

        var result = await service.VerifyAsync(token, CancellationToken.None);

        result.Should().BeFalse("错误的 issuer 应验签失败");
    }

    [Fact]
    public async Task VerifyAsync_With_Wrong_Audience_Should_Return_False()
    {
        var service = CreateService(signingMode: "Hs256", hs256Key: ValidHs256Key);

        var payload = new JwtPayload(
            issuer: TestIssuer,
            audience: "wrong-audience",
            claims: new[] { new Claim(JwtRegisteredClaimNames.Sub, TestSubject) },
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(30));

        var token = await service.SignAsync(payload, CancellationToken.None);

        var result = await service.VerifyAsync(token, CancellationToken.None);

        result.Should().BeFalse("错误的 audience 应验签失败");
    }

    [Fact]
    public async Task SignAsync_In_Rs256_Mode_Should_Include_KeyId_In_Header()
    {
        var (service, _) = CreateServiceWithRsa(signingMode: "Rs256", keyId: "key-v2");

        var payload = CreateValidPayload();
        var token = await service.SignAsync(payload, CancellationToken.None);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        jwt.Header.Kid.Should().Be("key-v2", "KeyId 应写入 kid 头便于验签方路由");
    }

    [Fact]
    public async Task SignAsync_With_Unknown_Mode_Should_Default_To_Hs256()
    {
        var service = CreateService(signingMode: "invalid-mode", hs256Key: ValidHs256Key);

        var payload = CreateValidPayload();
        var token = await service.SignAsync(payload, CancellationToken.None);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        jwt.Header.Alg.Should().Be("HS256", "未知签名模式应回退到 HS256");
    }

    [Fact]
    public void Constructor_With_Null_Kms_Should_Throw()
    {
        var act = () => new RsaJwtSigningService(
            null!,
            Options.Create(CreateOptions("Hs256")),
            Mock.Of<ILogger<RsaJwtSigningService>>());

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_With_Null_Options_Should_Throw()
    {
        var act = () => new RsaJwtSigningService(
            Mock.Of<IKeyManagementService>(),
            null!,
            Mock.Of<ILogger<RsaJwtSigningService>>());

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_With_Null_Logger_Should_Throw()
    {
        var act = () => new RsaJwtSigningService(
            Mock.Of<IKeyManagementService>(),
            Options.Create(CreateOptions("Hs256")),
            null!);

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>创建使用 Mock KMS（无需真实 RSA 密钥）的签名服务，适用于 HS256 模式测试。</summary>
    private static RsaJwtSigningService CreateService(
        string signingMode,
        string hs256Key = "",
        string keyId = TestKeyId)
    {
        var options = CreateOptions(signingMode, hs256Key, keyId);
        var kms = new Mock<IKeyManagementService>();
        return new RsaJwtSigningService(kms.Object, Options.Create(options), Mock.Of<ILogger<RsaJwtSigningService>>());
    }

    /// <summary>创建使用真实内存 RSA 密钥对的签名服务，适用于 RS256/Dual 模式测试。</summary>
    private static (RsaJwtSigningService service, RSA privateKey) CreateServiceWithRsa(
        string signingMode,
        string hs256Key = "",
        string keyId = TestKeyId)
    {
        var rsa = RSA.Create(2048);
        var options = CreateOptions(signingMode, hs256Key, keyId);
        var kms = new Mock<IKeyManagementService>();
        kms.Setup(k => k.GetPrivateKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(rsa);
        kms.Setup(k => k.GetPublicKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(rsa);
        var service = new RsaJwtSigningService(kms.Object, Options.Create(options), Mock.Of<ILogger<RsaJwtSigningService>>());
        return (service, rsa);
    }

    private static JwtSigningOptions CreateOptions(string signingMode, string hs256Key = "", string keyId = TestKeyId)
    {
        return new JwtSigningOptions
        {
            SigningMode = signingMode,
            CurrentKeyId = keyId,
            Hs256SigningKey = hs256Key,
            Issuer = TestIssuer,
            Audience = TestAudience,
            TokenTtlMinutes = 30
        };
    }

    private static JwtPayload CreateValidPayload()
    {
        var now = DateTime.UtcNow;
        return new JwtPayload(
            issuer: TestIssuer,
            audience: TestAudience,
            claims: new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, TestSubject),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            },
            notBefore: now,
            expires: now.AddMinutes(30));
    }
}
