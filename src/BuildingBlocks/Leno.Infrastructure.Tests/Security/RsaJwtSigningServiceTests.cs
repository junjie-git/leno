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
/// RsaJwtSigningService 单元测试（3.10 安全技术栈升级）。
/// <para>
/// 双轨下线 A6（2026-09-23，D-6 单算法）：原 Hs256/Dual 模式用例已随过渡机制删除，
/// 仅保留 RS256 签发/验签、kid 头、篡改与非法输入拒绝等用例，使用内存 RSA 密钥对。
/// </para>
/// </summary>
public class RsaJwtSigningServiceTests
{
    private const string TestIssuer = "leno-identity-test";
    private const string TestAudience = "leno-clients-test";
    private const string TestKeyId = "key-v1";
    private const string TestSubject = "user-123";

    [Fact]
    public async Task SignAsync_In_Rs256_Mode_Should_Produce_Valid_Jwt()
    {
        var (service, _) = CreateServiceWithRsa();

        var payload = CreateValidPayload();
        var token = await service.SignAsync(payload, CancellationToken.None);

        token.Should().NotBeNullOrEmpty();
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        jwt.Header.Alg.Should().Be("RS256");
    }

    [Fact]
    public async Task SignAsync_With_Null_Payload_Should_Throw()
    {
        var (service, _) = CreateServiceWithRsa();

        var act = async () => await service.SignAsync(null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task VerifyAsync_In_Rs256_Mode_Should_Validate_Correctly_Signed_Token()
    {
        var (service, _) = CreateServiceWithRsa();
        var payload = CreateValidPayload();
        var token = await service.SignAsync(payload, CancellationToken.None);

        var result = await service.VerifyAsync(token, CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyAsync_Should_Reject_Tampered_Token()
    {
        var (service, _) = CreateServiceWithRsa();
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
        var (service, _) = CreateServiceWithRsa();

        var result = await service.VerifyAsync(string.Empty, CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyAsync_With_Null_Token_Should_Return_False()
    {
        var (service, _) = CreateServiceWithRsa();

        var result = await service.VerifyAsync(null!, CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyAsync_With_Whitespace_Token_Should_Return_False()
    {
        var (service, _) = CreateServiceWithRsa();

        var result = await service.VerifyAsync("   ", CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyAsync_With_Expired_Token_Should_Return_False()
    {
        var (service, _) = CreateServiceWithRsa();

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
        var (service, _) = CreateServiceWithRsa();

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
        var (service, _) = CreateServiceWithRsa();

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
        var (service, _) = CreateServiceWithRsa(keyId: "key-v2");

        var payload = CreateValidPayload();
        var token = await service.SignAsync(payload, CancellationToken.None);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        jwt.Header.Kid.Should().Be("key-v2", "KeyId 应写入 kid 头便于验签方路由");
    }

    [Fact]
    public void Constructor_With_Null_Kms_Should_Throw()
    {
        var act = () => new RsaJwtSigningService(
            null!,
            Options.Create(CreateOptions()),
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
            Options.Create(CreateOptions()),
            null!);

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>创建使用真实内存 RSA 密钥对的签名服务。</summary>
    private static (RsaJwtSigningService service, RSA privateKey) CreateServiceWithRsa(string keyId = TestKeyId)
    {
        var rsa = RSA.Create(2048);
        var options = CreateOptions(keyId);
        var kms = new Mock<IKeyManagementService>();
        kms.Setup(k => k.GetPrivateKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(rsa);
        kms.Setup(k => k.GetPublicKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(rsa);
        var service = new RsaJwtSigningService(kms.Object, Options.Create(options), Mock.Of<ILogger<RsaJwtSigningService>>());
        return (service, rsa);
    }

    private static JwtSigningOptions CreateOptions(string keyId = TestKeyId)
    {
        return new JwtSigningOptions
        {
            CurrentKeyId = keyId,
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
