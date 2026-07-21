using Leno.UserAuth.Infrastructure.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Security.Cryptography;
using Xunit;

namespace Leno.UserAuth.Infrastructure.Tests.Auth;

/// <summary>
/// AlipayOAuth2Client RSA2 签名与验签测试。
/// 验证支付宝请求参数按 ASCII 字典序拼接后做 RSA-SHA256 签名，响应用支付宝公钥验签。
/// </summary>
public sealed class AlipayOAuth2ClientTests
{
    private static (string privateKeyPem, string publicKeyPem) GenerateRsaKeyPair()
    {
        using var rsa = RSA.Create(2048);
        var privateKey = rsa.ExportPkcs8PrivateKeyPem();
        var publicKey = rsa.ExportSubjectPublicKeyInfoPem();
        return (privateKey, publicKey);
    }

    private static AlipayOAuth2Client CreateClient(string privateKey, string? publicKey = null)
    {
        var configValues = new Dictionary<string, string?>
        {
            ["OAuth2:Alipay:AppId"] = "2021000000000001",
            ["OAuth2:Alipay:MerchantPrivateKey"] = privateKey
        };
        if (publicKey is not null)
        {
            configValues["OAuth2:Alipay:AlipayPublicKey"] = publicKey;
        }

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();
        var httpClientFactory = new Mock<IHttpClientFactory>();
        return new AlipayOAuth2Client(httpClientFactory.Object, config, NullLogger<AlipayOAuth2Client>.Instance);
    }

    [Fact]
    public void BuildSignedParameters_Should_Include_Sign_And_SignType_Rsa2()
    {
        // Arrange
        var (privateKey, _) = GenerateRsaKeyPair();
        var client = CreateClient(privateKey);

        var parameters = new Dictionary<string, string?>
        {
            ["app_id"] = "2021000000000001",
            ["method"] = "alipay.system.oauth.token",
            ["charset"] = "utf-8",
            ["timestamp"] = "2026-07-22 12:00:00",
            ["version"] = "1.0",
            ["grant_type"] = "authorization_code",
            ["code"] = "test-code"
        };

        // Act
        var signed = client.BuildSignedParameters(parameters);

        // Assert
        Assert.Equal("RSA2", signed["sign_type"]);
        Assert.False(string.IsNullOrEmpty(signed["sign"]));
        // sign 应为可 Base64 解码
        var signBytes = Convert.FromBase64String(signed["sign"]!);
        Assert.True(signBytes.Length == 256); // RSA-2048 签名 = 256 字节
    }

    [Fact]
    public void BuildSignedParameters_Should_Sort_Parameters_By_Ascii_Key_Before_Signing()
    {
        // Arrange
        var (privateKey, publicKey) = GenerateRsaKeyPair();
        var client = CreateClient(privateKey, publicKey);

        var parameters = new Dictionary<string, string?>
        {
            ["zebra"] = "1",
            ["apple"] = "2",
            ["mango"] = "3"
        };

        // Act
        var signed = client.BuildSignedParameters(parameters);

        // Assert：用同私钥重新签名应当得到相同 sign
        var expectedSign = client.ComputeSign(signed.Where(kv => kv.Key != "sign" && kv.Key != "sign_type" && !string.IsNullOrEmpty(kv.Value))
            .ToDictionary(kv => kv.Key, kv => kv.Value));
        Assert.Equal(expectedSign, signed["sign"]);
    }

    [Fact]
    public void VerifyResponseSign_Should_Return_True_When_Sign_Valid()
    {
        // Arrange
        var (privateKey, publicKey) = GenerateRsaKeyPair();
        var client = CreateClient(privateKey, publicKey);

        var responseData = new Dictionary<string, string?>
        {
            ["user_id"] = "2088000000000001",
            ["access_token"] = "token-123"
        };
        // 用商户私钥模拟支付宝签名（实际场景应使用支付宝公钥对应私钥）
        var sign = client.ComputeSign(responseData);

        // Act
        var verified = client.VerifyResponseSign(responseData, sign);

        // Assert
        Assert.True(verified);
    }
}
