using Leno.Infrastructure.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Leno.Infrastructure.Tests.Security;

/// <summary>
/// Argon2PasswordHasher 单元测试（3.10 安全技术栈升级）。
/// 覆盖哈希生成、校验、算法检测、pepper 注入、bcrypt 兼容校验与选项校验。
/// 使用低开销 Argon2id 参数保证测试速度（MemorySize=8KB, Iterations=1, Parallelism=1）。
/// </summary>
public class Argon2PasswordHasherTests
{
    private const string TestPassword = "MySecureP@ssw0rd!";
    private const string TestPepper = "test-pepper-secret";

    private static PasswordHashOptions CreateFastOptions() => new()
    {
        DegreeOfParallelism = 1,
        MemorySizeKB = 8,
        Iterations = 1,
        HashLengthBytes = 16,
        SaltLengthBytes = 8,
        Pepper = TestPepper
    };

    private static Argon2PasswordHasher CreateHasher(PasswordHashOptions? options = null, IPepperProvider? pepperProvider = null, BcryptPasswordVerifier? bcryptVerifier = null)
    {
        options ??= CreateFastOptions();
        pepperProvider ??= new ConstantPepperProvider(TestPepper);
        bcryptVerifier ??= new BcryptPasswordVerifier();
        var logger = Mock.Of<ILogger<Argon2PasswordHasher>>();
        return new Argon2PasswordHasher(Options.Create(options), pepperProvider, bcryptVerifier, logger);
    }

    [Fact]
    public void HashPassword_Should_Produce_Valid_Argon2id_PHC_Format()
    {
        var hasher = CreateHasher();

        var hash = hasher.HashPassword(TestPassword);

        hash.Should().NotBeNullOrEmpty();
        hash.Should().StartWith("$argon2id$");
        hash.Should().Contain("v=19$");
        hash.Should().Contain("m=8,t=1,p=1$");
    }

    [Fact]
    public void HashPassword_With_Empty_Password_Should_Throw()
    {
        var hasher = CreateHasher();

        var act = () => hasher.HashPassword(string.Empty);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("password");
    }

    [Fact]
    public void HashPassword_With_Null_Password_Should_Throw()
    {
        var hasher = CreateHasher();

        var act = () => hasher.HashPassword(null!);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("password");
    }

    [Fact]
    public void HashPassword_Should_Produce_Different_Hashes_For_Same_Password()
    {
        var hasher = CreateHasher();

        var hash1 = hasher.HashPassword(TestPassword);
        var hash2 = hasher.HashPassword(TestPassword);

        hash1.Should().NotBe(hash2, "每次哈希使用随机盐，相同密码应产生不同哈希");
    }

    [Fact]
    public void VerifyPassword_With_Correct_Password_Should_Return_True()
    {
        var hasher = CreateHasher();
        var hash = hasher.HashPassword(TestPassword);

        var result = hasher.VerifyPassword(TestPassword, hash);

        result.Should().BeTrue();
    }

    [Fact]
    public void VerifyPassword_With_Wrong_Password_Should_Return_False()
    {
        var hasher = CreateHasher();
        var hash = hasher.HashPassword(TestPassword);

        var result = hasher.VerifyPassword("WrongPassword123", hash);

        result.Should().BeFalse();
    }

    [Fact]
    public void VerifyPassword_With_Empty_Password_Should_Return_False()
    {
        var hasher = CreateHasher();
        var hash = hasher.HashPassword(TestPassword);

        var result = hasher.VerifyPassword(string.Empty, hash);

        result.Should().BeFalse();
    }

    [Fact]
    public void VerifyPassword_With_Empty_Hash_Should_Return_False()
    {
        var hasher = CreateHasher();

        var result = hasher.VerifyPassword(TestPassword, string.Empty);

        result.Should().BeFalse();
    }

    [Fact]
    public void VerifyPassword_With_Malformed_Argon2_Hash_Should_Return_False()
    {
        var hasher = CreateHasher();
        var malformedHash = "$argon2id$v=19$m=8,t=1,p=1$invalid-base64$also-invalid";

        var result = hasher.VerifyPassword(TestPassword, malformedHash);

        result.Should().BeFalse("格式错误的 Argon2id 哈希应安全返回 false 而非抛异常");
    }

    [Fact]
    public void VerifyPassword_Should_Verify_Without_Pepper_When_Pepper_Changes()
    {
        // 哈希时使用 pepper A，校验时使用 pepper B → 应校验失败
        var hash = CreateHasher(pepperProvider: new ConstantPepperProvider("pepper-A"))
            .HashPassword(TestPassword);

        var hasherWithDifferentPepper = CreateHasher(pepperProvider: new ConstantPepperProvider("pepper-B"));

        var result = hasherWithDifferentPepper.VerifyPassword(TestPassword, hash);

        result.Should().BeFalse("pepper 不一致时校验应失败");
    }

    [Fact]
    public void VerifyPassword_Should_Verify_Bcrypt_Legacy_Hash()
    {
        // 使用真实的 BCrypt.Net 生成一个 bcrypt 哈希
        var bcryptHash = BCrypt.Net.BCrypt.HashPassword(TestPassword, workFactor: 4);
        var hasher = CreateHasher();

        var result = hasher.VerifyPassword(TestPassword, bcryptHash);

        result.Should().BeTrue("应兼容校验历史 bcrypt 哈希");
    }

    [Fact]
    public void VerifyPassword_With_Wrong_Password_Against_Bcrypt_Hash_Should_Return_False()
    {
        var bcryptHash = BCrypt.Net.BCrypt.HashPassword(TestPassword, workFactor: 4);
        var hasher = CreateHasher();

        var result = hasher.VerifyPassword("WrongPassword", bcryptHash);

        result.Should().BeFalse();
    }

    [Fact]
    public void DetectAlgorithm_With_Argon2id_Hash_Should_Return_Argon2id()
    {
        var hasher = CreateHasher();
        var hash = hasher.HashPassword(TestPassword);

        var algorithm = hasher.DetectAlgorithm(hash);

        algorithm.Should().Be(PasswordHashAlgorithm.Argon2id);
    }

    [Theory]
    [InlineData("$2a$")]
    [InlineData("$2b$")]
    [InlineData("$2y$")]
    public void DetectAlgorithm_With_Bcrypt_Hash_Should_Return_Bcrypt(string prefix)
    {
        var hasher = CreateHasher();
        var bcryptHash = BCrypt.Net.BCrypt.HashPassword(TestPassword, workFactor: 4);

        var algorithm = hasher.DetectAlgorithm(bcryptHash);

        algorithm.Should().Be(PasswordHashAlgorithm.Bcrypt);
    }

    [Fact]
    public void DetectAlgorithm_With_Unknown_Format_Should_Throw_FormatException()
    {
        var hasher = CreateHasher();

        var act = () => hasher.DetectAlgorithm("$unknown$format$hash");

        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void DetectAlgorithm_With_Empty_Hash_Should_Throw()
    {
        var hasher = CreateHasher();

        var act = () => hasher.DetectAlgorithm(string.Empty);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("hash");
    }

    [Fact]
    public void Constructor_With_DegreeOfParallelism_Less_Than_1_Should_Throw()
    {
        var options = CreateFastOptions();
        options.DegreeOfParallelism = 0;

        var act = () => CreateHasher(options);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*DegreeOfParallelism*");
    }

    [Fact]
    public void Constructor_With_MemorySize_Less_Than_8_Should_Throw()
    {
        var options = CreateFastOptions();
        options.MemorySizeKB = 4;

        var act = () => CreateHasher(options);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*MemorySizeKB*");
    }

    [Fact]
    public void Constructor_With_Iterations_Less_Than_1_Should_Throw()
    {
        var options = CreateFastOptions();
        options.Iterations = 0;

        var act = () => CreateHasher(options);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Iterations*");
    }

    [Fact]
    public void Constructor_With_HashLength_Less_Than_16_Should_Throw()
    {
        var options = CreateFastOptions();
        options.HashLengthBytes = 8;

        var act = () => CreateHasher(options);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*HashLengthBytes*");
    }

    [Fact]
    public void Constructor_With_SaltLength_Less_Than_8_Should_Throw()
    {
        var options = CreateFastOptions();
        options.SaltLengthBytes = 4;

        var act = () => CreateHasher(options);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*SaltLengthBytes*");
    }

    [Fact]
    public void Constructor_With_Null_Options_Should_Throw()
    {
        var act = () => new Argon2PasswordHasher(
            null!,
            new ConstantPepperProvider(TestPepper),
            new BcryptPasswordVerifier(),
            Mock.Of<ILogger<Argon2PasswordHasher>>());

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_With_Null_PepperProvider_Should_Throw()
    {
        var act = () => new Argon2PasswordHasher(
            Options.Create(CreateFastOptions()),
            null!,
            new BcryptPasswordVerifier(),
            Mock.Of<ILogger<Argon2PasswordHasher>>());

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_With_Null_BcryptVerifier_Should_Throw()
    {
        var act = () => new Argon2PasswordHasher(
            Options.Create(CreateFastOptions()),
            new ConstantPepperProvider(TestPepper),
            null!,
            Mock.Of<ILogger<Argon2PasswordHasher>>());

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_With_Null_Logger_Should_Throw()
    {
        var act = () => new Argon2PasswordHasher(
            Options.Create(CreateFastOptions()),
            new ConstantPepperProvider(TestPepper),
            new BcryptPasswordVerifier(),
            null!);

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>固定返回指定 pepper 值的测试用 IPepperProvider。</summary>
    private sealed class ConstantPepperProvider : IPepperProvider
    {
        private readonly string _pepper;

        public ConstantPepperProvider(string pepper)
        {
            _pepper = pepper;
        }

        public string GetPepper() => _pepper;
    }
}
