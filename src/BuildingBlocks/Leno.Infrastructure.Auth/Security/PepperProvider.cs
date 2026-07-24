using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.Infrastructure.Security;

/// <summary>
/// <see cref="IPepperProvider"/> 默认实现（3.10 安全技术栈升级）。
/// <para>
/// 解析优先级：
/// <list type="number">
/// <item>KMS 解包：当 <see cref="PasswordHashOptions.UseKmsForPepper"/> 为 true 且 <see cref="PasswordHashOptions.WrappedPepper"/> 非空时，经 <see cref="IKeyManagementService.UnwrapAesKeyAsync"/> 解包。</item>
/// <item>环境变量 <c>PASSWORD_PEPPER</c>。</item>
/// <item><see cref="PasswordHashOptions.Pepper"/> 静态配置。</item>
/// </list>
/// KMS 解包失败时自动回退到环境变量与静态配置（DG-4 务实推进）。
/// 结果在实例生命周期内缓存（<see cref="Lazy{T}"/>），避免每次校验重复 KMS 调用。
/// </para>
/// </summary>
public sealed class PepperProvider : IPepperProvider
{
    private const string PepperEnvVar = "PASSWORD_PEPPER";

    private readonly IKeyManagementService? _kms;
    private readonly PasswordHashOptions _options;
    private readonly ILogger<PepperProvider> _logger;
    private readonly Lazy<string> _pepper;

    public PepperProvider(
        IOptions<PasswordHashOptions> options,
        ILogger<PepperProvider> logger,
        IKeyManagementService? kms = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options.Value ?? new PasswordHashOptions();
        _logger = logger;
        _kms = kms;
        _pepper = new Lazy<string>(ResolvePepper, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <inheritdoc />
    public string GetPepper() => _pepper.Value;

    private string ResolvePepper()
    {
        // 1. KMS 解包（配置启用且 KMS 可用）
        if (_options.UseKmsForPepper && _kms is not null && !string.IsNullOrWhiteSpace(_options.WrappedPepper))
        {
            try
            {
                var unwrapped = _kms.UnwrapAesKeyAsync(_options.WrappedPepper, CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
                var pepper = System.Text.Encoding.UTF8.GetString(unwrapped);
                _logger.LogInformation("Pepper 已通过 KMS 解包获取");
                return pepper;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "KMS 解包 pepper 失败，回退到环境变量与静态配置");
            }
        }

        // 2. 环境变量
        var envPepper = Environment.GetEnvironmentVariable(PepperEnvVar);
        if (!string.IsNullOrEmpty(envPepper))
        {
            _logger.LogInformation("Pepper 已从环境变量 {EnvVar} 获取", PepperEnvVar);
            return envPepper;
        }

        // 3. 静态配置
        _logger.LogWarning("Pepper 使用静态配置值（仅限开发环境，生产应使用 KMS 或环境变量）");
        return _options.Pepper ?? string.Empty;
    }
}
