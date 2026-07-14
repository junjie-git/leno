using Microsoft.Extensions.Logging;

namespace Leno.ApiGateway.Transforms;

/// <summary>
/// 协议转换注册表。通过 DI 收集所有 <see cref="IProtocolTranslator"/> 实现，
/// 按 <c>(SourceProtocol, TargetProtocol)</c> 查找（大小写不敏感）。
/// <para>
/// 在 YARP 管道预留注入点：当后端服务提供 gRPC 端点后，
/// 注册对应 <see cref="IProtocolTranslator"/> 实现并在此查找即可启用协议转换。
/// </para>
/// </summary>
public sealed class ProtocolTranslatorRegistry
{
    private readonly Dictionary<(string Source, string Target), IProtocolTranslator> _translators;
    private readonly ILogger<ProtocolTranslatorRegistry> _logger;

    public ProtocolTranslatorRegistry(
        IEnumerable<IProtocolTranslator> translators,
        ILogger<ProtocolTranslatorRegistry> logger)
    {
        ArgumentNullException.ThrowIfNull(translators);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _translators = new Dictionary<(string, string), IProtocolTranslator>();

        foreach (var translator in translators)
        {
            var key = (
                translator.SourceProtocol.ToUpperInvariant(),
                translator.TargetProtocol.ToUpperInvariant()
            );

            if (_translators.ContainsKey(key))
            {
                _logger.LogWarning(
                    "Duplicate protocol translator for {Source}->{Target}, overwriting",
                    translator.SourceProtocol, translator.TargetProtocol);
            }

            _translators[key] = translator;
        }
    }

    /// <summary>
    /// 按源/目标协议查找转换器（大小写不敏感）。
    /// </summary>
    public IProtocolTranslator? Find(string sourceProtocol, string targetProtocol)
    {
        if (string.IsNullOrEmpty(sourceProtocol) || string.IsNullOrEmpty(targetProtocol))
        {
            return null;
        }

        var key = (sourceProtocol.ToUpperInvariant(), targetProtocol.ToUpperInvariant());
        return _translators.TryGetValue(key, out var translator) ? translator : null;
    }

    /// <summary>所有已注册的协议转换器。</summary>
    public IReadOnlyCollection<IProtocolTranslator> All => _translators.Values;
}
