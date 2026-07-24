using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.Infrastructure.Security;

/// <summary>
/// Argon2id 密码哈希器（3.10 安全技术栈升级），替代 bcrypt。
/// <para>
/// 特性：
/// <list type="bullet">
/// <item>使用 <c>Konscious.Security.Cryptography.Argon2id</c> 算法。</item>
/// <item>自动注入 pepper（pepper + password 拼接后哈希）。</item>
/// <item>每次哈希生成随机 16 字节盐。</item>
/// <item>PHC 格式输出：<c>$argon2id$v=19$m={memory},t={iterations},p={parallelism}${base64(salt)}${base64(hash)}</c>。</item>
/// <item>兼容校验旧 bcrypt 哈希（通过 <see cref="BcryptPasswordVerifier"/> 委托）。</item>
/// <item>校验使用 <see cref="CryptographicOperations.FixedTimeEquals"/> 防时序侧信道。</item>
/// </list>
/// </para>
/// </summary>
public sealed class Argon2PasswordHasher : IPasswordHasher
{
    private const string Argon2idPrefix = "$argon2id$";
    private const int Argon2Version = 19;

    private readonly PasswordHashOptions _options;
    private readonly IPepperProvider _pepperProvider;
    private readonly BcryptPasswordVerifier _bcryptVerifier;
    private readonly ILogger<Argon2PasswordHasher> _logger;

    public Argon2PasswordHasher(
        IOptions<PasswordHashOptions> options,
        IPepperProvider pepperProvider,
        BcryptPasswordVerifier bcryptVerifier,
        ILogger<Argon2PasswordHasher> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(pepperProvider);
        ArgumentNullException.ThrowIfNull(bcryptVerifier);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options.Value ?? new PasswordHashOptions();
        _pepperProvider = pepperProvider;
        _bcryptVerifier = bcryptVerifier;
        _logger = logger;

        ValidateOptions();
    }

    /// <inheritdoc />
    public string HashPassword(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            throw new ArgumentException("明文密码不可为空", nameof(password));
        }

        var pepper = _pepperProvider.GetPepper();
        var pepperedPassword = ApplyPepper(password, pepper);
        var passwordBytes = Encoding.UTF8.GetBytes(pepperedPassword);

        var salt = new byte[_options.SaltLengthBytes];
        RandomNumberGenerator.Fill(salt);

        var hash = ComputeArgon2id(
            passwordBytes,
            salt,
            _options.MemorySizeKB,
            _options.Iterations,
            _options.DegreeOfParallelism,
            _options.HashLengthBytes);

        // 清除敏感数据
        CryptographicOperations.ZeroMemory(passwordBytes);

        var saltB64 = Base64Encode(salt);
        var hashB64 = Base64Encode(hash);

        return $"{Argon2idPrefix}v={Argon2Version}$m={_options.MemorySizeKB},t={_options.Iterations},p={_options.DegreeOfParallelism}${saltB64}${hashB64}";
    }

    /// <inheritdoc />
    public bool VerifyPassword(string password, string hash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hash))
        {
            return false;
        }

        var algorithm = DetectAlgorithm(hash);
        return algorithm switch
        {
            PasswordHashAlgorithm.Argon2id => VerifyArgon2(password, hash),
            PasswordHashAlgorithm.Bcrypt => VerifyBcrypt(password, hash),
            _ => false
        };
    }

    /// <inheritdoc />
    public PasswordHashAlgorithm DetectAlgorithm(string hash)
    {
        if (string.IsNullOrEmpty(hash))
        {
            throw new ArgumentException("哈希字符串不可为空", nameof(hash));
        }

        if (hash.StartsWith(Argon2idPrefix, StringComparison.Ordinal))
        {
            return PasswordHashAlgorithm.Argon2id;
        }

        // bcrypt 前缀：$2a$ / $2b$ / $2y$
        if (hash.StartsWith("$2a$", StringComparison.Ordinal) ||
            hash.StartsWith("$2b$", StringComparison.Ordinal) ||
            hash.StartsWith("$2y$", StringComparison.Ordinal))
        {
            return PasswordHashAlgorithm.Bcrypt;
        }

        throw new FormatException($"无法识别的密码哈希格式：{hash[..Math.Min(hash.Length, 16)]}...");
    }

    private bool VerifyArgon2(string password, string hash)
    {
        try
        {
            var parsed = ParsePhcFormat(hash);

            var pepper = _pepperProvider.GetPepper();
            var pepperedPassword = ApplyPepper(password, pepper);
            var passwordBytes = Encoding.UTF8.GetBytes(pepperedPassword);

            var computedHash = ComputeArgon2id(
                passwordBytes,
                parsed.Salt,
                parsed.MemorySizeKB,
                parsed.Iterations,
                parsed.DegreeOfParallelism,
                parsed.Hash.Length);

            CryptographicOperations.ZeroMemory(passwordBytes);

            return CryptographicOperations.FixedTimeEquals(computedHash, parsed.Hash);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException)
        {
            _logger.LogWarning(ex, "Argon2id 哈希解析失败");
            return false;
        }
    }

    private bool VerifyBcrypt(string password, string hash)
    {
        // bcrypt 历史哈希不含 pepper（3.10 之前的 bcrypt 未使用 pepper）
        // 直接用原密码校验，不注入 pepper
        return _bcryptVerifier.Verify(password, hash);
    }

    private static byte[] ComputeArgon2id(
        byte[] passwordBytes,
        byte[] salt,
        int memorySizeKB,
        int iterations,
        int degreeOfParallelism,
        int hashLengthBytes)
    {
        using var argon2 = new Argon2id(passwordBytes);
        argon2.Salt = salt;
        argon2.MemorySize = memorySizeKB;
        argon2.Iterations = iterations;
        argon2.DegreeOfParallelism = degreeOfParallelism;
        return argon2.GetBytes(hashLengthBytes);
    }

    private static string ApplyPepper(string password, string pepper)
    {
        if (string.IsNullOrEmpty(pepper))
        {
            return password;
        }

        // pepper 前置拼接：pepper + password
        return string.Concat(pepper, password);
    }

    private static string Base64Encode(byte[] data)
    {
        return Convert.ToBase64String(data)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private static byte[] Base64Decode(string encoded)
    {
        var padded = encoded.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + ((4 - padded.Length % 4) % 4), '=');
        return Convert.FromBase64String(padded);
    }

    private static ParsedPhc ParsePhcFormat(string hash)
    {
        // 格式：$argon2id$v=19$m=65536,t=3,p=4$base64salt$base64hash
        var parts = hash.Split('$');
        if (parts.Length != 6)
        {
            throw new FormatException($"Argon2id PHC 格式不正确，期望 6 段，实际 {parts.Length} 段");
        }

        if (parts[1] != "argon2id")
        {
            throw new FormatException($"不是 Argon2id 哈希：{parts[1]}");
        }

        // parts[2] = "v=19"
        if (!parts[2].StartsWith("v=", StringComparison.Ordinal))
        {
            throw new FormatException($"Argon2id 版本段格式不正确：{parts[2]}");
        }
        var version = int.Parse(parts[2][2..], System.Globalization.CultureInfo.InvariantCulture);
        if (version != Argon2Version)
        {
            throw new FormatException($"不支持的 Argon2id 版本：{version}");
        }

        // parts[3] = "m=65536,t=3,p=4"
        var paramParts = parts[3].Split(',');
        if (paramParts.Length != 3)
        {
            throw new FormatException($"Argon2id 参数段格式不正确：{parts[3]}");
        }

        int memorySizeKB = 0, iterations = 0, degreeOfParallelism = 0;
        foreach (var param in paramParts)
        {
            var kv = param.Split('=');
            if (kv.Length != 2)
            {
                throw new FormatException($"Argon2id 参数格式不正确：{param}");
            }

            var value = int.Parse(kv[1], System.Globalization.CultureInfo.InvariantCulture);
            switch (kv[0])
            {
                case "m": memorySizeKB = value; break;
                case "t": iterations = value; break;
                case "p": degreeOfParallelism = value; break;
                default: throw new FormatException($"未知的 Argon2id 参数：{kv[0]}");
            }
        }

        var salt = Base64Decode(parts[4]);
        var hashBytes = Base64Decode(parts[5]);

        return new ParsedPhc(memorySizeKB, iterations, degreeOfParallelism, salt, hashBytes);
    }

    private void ValidateOptions()
    {
        if (_options.DegreeOfParallelism < 1)
        {
            throw new InvalidOperationException($"DegreeOfParallelism 必须 ≥ 1，当前为 {_options.DegreeOfParallelism}");
        }

        if (_options.MemorySizeKB < 8)
        {
            throw new InvalidOperationException($"MemorySizeKB 必须 ≥ 8，当前为 {_options.MemorySizeKB}");
        }

        if (_options.Iterations < 1)
        {
            throw new InvalidOperationException($"Iterations 必须 ≥ 1，当前为 {_options.Iterations}");
        }

        if (_options.HashLengthBytes < 16)
        {
            throw new InvalidOperationException($"HashLengthBytes 必须 ≥ 16，当前为 {_options.HashLengthBytes}");
        }

        if (_options.SaltLengthBytes < 8)
        {
            throw new InvalidOperationException($"SaltLengthBytes 必须 ≥ 8，当前为 {_options.SaltLengthBytes}");
        }
    }

    private sealed record ParsedPhc(
        int MemorySizeKB,
        int Iterations,
        int DegreeOfParallelism,
        byte[] Salt,
        byte[] Hash);
}
