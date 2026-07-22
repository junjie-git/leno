using Leno.UserAuth.Application.Services;

namespace Leno.UserAuth.Application.Tests;

public class SecureTokenGeneratorTests
{
    #region UrlSafe & No Padding

    [Fact]
    public void GenerateSecureToken_Should_Be_UrlSafe_And_No_Padding()
    {
        // 多次采样，确保任意随机字节组合都不会产生非 URL 安全字符或 padding。
        for (var i = 0; i < 200; i++)
        {
            var token = UserAppService.GenerateSecureToken(32);

            token.Should().NotBeNullOrEmpty();
            token.Should().NotContain("=", "Base64 padding '=' 应被移除");
            token.Should().NotContain("+", "Base64 '+' 应替换为 URL 安全的 '-'");
            token.Should().NotContain("/", "Base64 '/' 应替换为 URL 安全的 '_'");
        }
    }

    [Fact]
    public void GenerateSecureToken_Should_Only_Contain_UrlSafe_Characters()
    {
        // Base64url 字母表：A-Z a-z 0-9 - _
        const string AllowedChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";

        for (var i = 0; i < 100; i++)
        {
            var token = UserAppService.GenerateSecureToken(32);
            foreach (var c in token)
            {
                AllowedChars.Should().Contain(c.ToString(),
                    $"令牌应仅包含 Base64url 字母表字符，但发现非法字符 '{c}'");
            }
        }
    }

    #endregion

    #region High Entropy

    [Fact]
    public void GenerateSecureToken_Should_Have_High_Entropy()
    {
        // 统计分布测试：生成 10000 个 32 字节令牌，全部应唯一（碰撞概率极低）。
        // 256 位熵空间下 10000 次采样的碰撞概率约为 n^2/(2*2^256) ≈ 4.6e-71，远低于偶然阈值。
        var tokens = new HashSet<string>();
        for (var i = 0; i < 10000; i++)
        {
            tokens.Add(UserAppService.GenerateSecureToken(32));
        }

        tokens.Count.Should().Be(10000, "10000 次 256 位熵采样应全部唯一，无碰撞");
    }

    [Fact]
    public void GenerateSecureToken_Should_Produce_Different_Values_Each_Call()
    {
        var token1 = UserAppService.GenerateSecureToken();
        var token2 = UserAppService.GenerateSecureToken();

        token1.Should().NotBe(token2, "连续两次调用应产生不同令牌");
    }

    #endregion

    #region Length & Byte Length

    [Fact]
    public void GenerateSecureToken_With_Default_ByteLength_Should_Have_Expected_Length()
    {
        // 32 字节 → Base64 编码 44 字符（含 padding），去除 padding 后 43 字符。
        var token = UserAppService.GenerateSecureToken();

        token.Length.Should().Be(43, "32 字节 Base64url 无 padding 应为 43 字符");
    }

    [Theory]
    [InlineData(16, 22)]  // 128 位 → 22 字符
    [InlineData(32, 43)]  // 256 位 → 43 字符
    [InlineData(48, 64)]  // 384 位 → 64 字符
    [InlineData(64, 86)]  // 512 位 → 86 字符
    public void GenerateSecureToken_With_Custom_ByteLength_Should_Have_Correct_Length(
        int byteLength, int expectedCharLength)
    {
        // Base64url 无 padding 长度 = ceil(byteLength * 4 / 3)
        var token = UserAppService.GenerateSecureToken(byteLength);

        token.Length.Should().Be(expectedCharLength);
    }

    #endregion

    #region Validation

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void GenerateSecureToken_With_Invalid_ByteLength_Should_Throw(int byteLength)
    {
        var act = () => UserAppService.GenerateSecureToken(byteLength);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("byteLength");
    }

    [Fact]
    public void GenerateSecureToken_With_One_Byte_Should_Return_Two_Chars()
    {
        // 1 字节 = 8 位，Base64 每 6 位一字符 → ceil(8/6) = 2 字符（无 padding）
        var token = UserAppService.GenerateSecureToken(1);

        token.Length.Should().Be(2, "1 字节 Base64url 无 padding 应为 2 字符");
        token.Should().NotBeEmpty();
    }

    #endregion

    #region Reuse Consistency

    [Fact]
    public void GenerateSecureToken_Should_Be_Suitable_For_OAuth_State_Usage()
    {
        // 模拟 OAuth state 场景：令牌需作为 URL 查询参数传递，
        // 验证生成的令牌不含需要 URL 编码的字符。
        for (var i = 0; i < 50; i++)
        {
            var state = UserAppService.GenerateSecureToken(32);

            // 模拟 URL 查询参数编码后应与原值一致（无特殊字符需编码）
            var encoded = Uri.EscapeDataString(state);
            encoded.Should().Be(state, "Base64url 令牌不应包含需 URL 编码的字符");
        }
    }

    #endregion
}
