using System.Security.Cryptography;
using Leno.UserAuth.Infrastructure.Services;

namespace Leno.UserAuth.Infrastructure.Tests.Services;

/// <summary>
/// AesEncryptionService 单元测试，验证 AES-GCM 认证加密的正确性、随机 Nonce 与防篡改能力。
/// </summary>
public sealed class AesEncryptionServiceTests
{
    private static readonly byte[] Key = Convert.FromBase64String("AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=");

    [Fact]
    public void Encrypt_Then_Decrypt_Should_Roundtrip_Original_Plaintext()
    {
        var service = new AesEncryptionService(Convert.ToBase64String(Key));
        var plain = "my-oauth-client-secret-12345";

        var cipher = service.Encrypt(plain);
        var decrypted = service.Decrypt(cipher);

        Assert.Equal(plain, decrypted);
    }

    [Fact]
    public void Encrypt_Should_Produce_Different_Ciphertext_For_Same_Plaintext_Due_To_Random_Nonce()
    {
        var service = new AesEncryptionService(Convert.ToBase64String(Key));
        var plain = "same-secret";

        var c1 = service.Encrypt(plain);
        var c2 = service.Encrypt(plain);

        Assert.NotEqual(c1, c2);
    }

    [Fact]
    public void Decrypt_Should_Throw_When_Ciphertext_Tampered()
    {
        var service = new AesEncryptionService(Convert.ToBase64String(Key));
        var cipher = service.Encrypt("secret");

        // 篡改密文末尾
        var bytes = Convert.FromBase64String(cipher);
        bytes[^1] ^= 0xFF;
        var tampered = Convert.ToBase64String(bytes);

        Assert.ThrowsAny<CryptographicException>(() => service.Decrypt(tampered));
    }

    [Fact]
    public void Decrypted_Ciphertext_Length_Should_Be_At_Least_Nonce_Plus_Tag()
    {
        var service = new AesEncryptionService(Convert.ToBase64String(Key));
        var cipher = service.Encrypt("x");
        var bytes = Convert.FromBase64String(cipher);

        // GCM: nonce(12) + ciphertext + tag(16)，最少 12 + 1 + 16 = 29
        Assert.True(bytes.Length >= 29);
    }
}
