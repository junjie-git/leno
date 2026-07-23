namespace Leno.Infrastructure.Security;

/// <summary>
/// Argon2id 密码哈希配置，对应 appsettings.json 中 <c>PasswordHash</c> 节。
/// </summary>
public sealed class PasswordHashOptions
{
    /// <summary>并行度（线程数），Argon2id 推荐值 4。</summary>
    public int DegreeOfParallelism { get; set; } = 4;

    /// <summary>内存大小（KB），Argon2id 推荐值 65536（64 MB）。</summary>
    public int MemorySizeKB { get; set; } = 65536;

    /// <summary>迭代次数，Argon2id 推荐值 3。</summary>
    public int Iterations { get; set; } = 3;

    /// <summary>哈希输出长度（字节），默认 32（256 位）。</summary>
    public int HashLengthBytes { get; set; } = 32;

    /// <summary>盐长度（字节），默认 16（128 位）。</summary>
    public int SaltLengthBytes { get; set; } = 16;

    /// <summary>
    /// 静态 pepper 值（直接配置）。优先级低于环境变量 <c>PASSWORD_PEPPER</c> 与 KMS。
    /// </summary>
    public string Pepper { get; set; } = string.Empty;

    /// <summary>是否通过 KMS 解包获取 pepper。为 true 时使用 <see cref="WrappedPepper"/> 经 KMS 解包。</summary>
    public bool UseKmsForPepper { get; set; } = false;

    /// <summary>
    /// KMS 包装的 pepper（Base64 字符串）。当 <see cref="UseKmsForPepper"/> 为 true 时，
    /// 经 <see cref="IKeyManagementService.UnwrapAesKeyAsync"/> 解包得到 pepper 原文。
    /// </summary>
    public string WrappedPepper { get; set; } = string.Empty;

    /// <summary>配置节名称。</summary>
    public const string SectionName = "PasswordHash";
}
