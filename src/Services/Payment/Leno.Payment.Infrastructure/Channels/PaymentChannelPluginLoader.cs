using System.Reflection;
using Leno.Payment.Domain.Services;
using Leno.Payment.Infrastructure.Config;
using Microsoft.Extensions.Logging;

namespace Leno.Payment.Infrastructure.Channels;

/// <summary>
/// 支付渠道插件加载结果，包含成功加载的适配器类型与失败项。
/// </summary>
public sealed class PaymentChannelPluginLoadResult
{
    /// <summary>成功识别的适配器类型列表（已实现 <see cref="IPaymentChannelAdapter"/> 且非抽象）。</summary>
    public IReadOnlyList<Type> AdapterTypes { get; init; } = Array.Empty<Type>();

    /// <summary>加载失败的程序集路径与失败原因。</summary>
    public IReadOnlyList<PluginLoadFailure> Failures { get; init; } = Array.Empty<PluginLoadFailure>();
}

/// <summary>
/// 插件加载失败项。
/// </summary>
public sealed record PluginLoadFailure(string AssemblyPath, string Reason, Exception? Exception);

/// <summary>
/// 支付渠道插件加载器，通过 <see cref="Assembly.LoadFrom"/> 动态加载外部程序集并扫描其中实现
/// <see cref="IPaymentChannelAdapter"/> 的非抽象类型，供 DI 注册为 <c>IEnumerable&lt;IPaymentChannelAdapter&gt;</c>。
/// </summary>
/// <remarks>
/// 阶段三 3.8：新增渠道（如 UnionPay / ApplePay）可打包为独立 dll 放入插件目录，
/// 在 <c>appsettings.json</c> 的 <c>Payment:Plugins:PluginAssemblies</c> 节配置路径即可被加载，
/// 无需修改 Payment BC 主代码与工厂分支。
///
/// 安全约束：
/// <list type="bullet">
/// <item>加载前校验文件路径存在且为 <c>.dll</c> 扩展名。</item>
/// <item>仅注册实现 <see cref="IPaymentChannelAdapter"/> 且非抽象的公共类型。</item>
/// <item>加载异常不抛出，记录到 <see cref="PaymentChannelPluginLoadResult.Failures"/>，避免单个插件故障阻塞启动。</item>
/// </list>
/// </remarks>
public sealed class PaymentChannelPluginLoader
{
    private readonly ILogger<PaymentChannelPluginLoader> _logger;

    public PaymentChannelPluginLoader(ILogger<PaymentChannelPluginLoader> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 加载插件程序集并扫描适配器类型。
    /// </summary>
    /// <param name="options">插件配置选项。</param>
    /// <returns>加载结果，包含适配器类型与失败项。</returns>
    public PaymentChannelPluginLoadResult Load(PaymentChannelPluginOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var adapterTypes = new List<Type>();
        var failures = new List<PluginLoadFailure>();

        if (options.PluginAssemblies.Count == 0)
        {
            return new PaymentChannelPluginLoadResult
            {
                AdapterTypes = adapterTypes,
                Failures = failures
            };
        }

        foreach (var rawPath in options.PluginAssemblies)
        {
            if (string.IsNullOrWhiteSpace(rawPath))
            {
                failures.Add(new PluginLoadFailure(rawPath ?? string.Empty, "插件程序集路径为空", null));
                continue;
            }

            try
            {
                var fullPath = Path.GetFullPath(rawPath);

                if (!File.Exists(fullPath))
                {
                    failures.Add(new PluginLoadFailure(fullPath, "插件程序集文件不存在", null));
                    _logger.LogWarning("支付渠道插件加载：文件不存在 Path={Path}", fullPath);
                    continue;
                }

                if (!string.Equals(Path.GetExtension(fullPath), ".dll", StringComparison.OrdinalIgnoreCase))
                {
                    failures.Add(new PluginLoadFailure(fullPath, "插件程序集扩展名必须为 .dll", null));
                    _logger.LogWarning("支付渠道插件加载：扩展名非 .dll Path={Path}", fullPath);
                    continue;
                }

                var assembly = Assembly.LoadFrom(fullPath);
                var types = ScanAdapterTypes(assembly);
                adapterTypes.AddRange(types);

                _logger.LogInformation(
                    "支付渠道插件加载：程序集 {Assembly} 识别到 {Count} 个适配器类型",
                    assembly.GetName().Name, types.Count);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failures.Add(new PluginLoadFailure(rawPath, $"加载程序集异常：{ex.Message}", ex));
                _logger.LogError(ex, "支付渠道插件加载异常 Path={Path}", rawPath);
            }
        }

        return new PaymentChannelPluginLoadResult
        {
            AdapterTypes = adapterTypes,
            Failures = failures
        };
    }

    /// <summary>
    /// 扫描程序集中实现 <see cref="IPaymentChannelAdapter"/> 且非抽象的公共类型。
    /// </summary>
    private List<Type> ScanAdapterTypes(Assembly assembly)
    {
        var result = new List<Type>();
        Type[] types;

        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            // 部分类型加载失败时仍处理已成功加载的类型
            types = ex.Types?.Where(t => t is not null).Select(t => t!).ToArray() ?? Type.EmptyTypes;
            _logger.LogWarning(ex,
                "支付渠道插件加载：程序集 {Assembly} 部分类型加载失败，已加载 {Count} 个类型",
                assembly.GetName().Name, types.Length);
        }

        foreach (var type in types)
        {
            if (type is null) continue;
            if (!typeof(IPaymentChannelAdapter).IsAssignableFrom(type)) continue;
            if (type.IsAbstract) continue;
            if (type.IsInterface) continue;
            if (!type.IsClass) continue;
            if (!type.IsPublic) continue;

            result.Add(type);
        }

        return result;
    }
}
