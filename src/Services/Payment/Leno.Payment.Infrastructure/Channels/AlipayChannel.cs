using Leno.Payment.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Leno.Payment.Infrastructure.Channels;

/// <summary>
/// 支付宝渠道，封装支付宝回调签名验证逻辑。
/// 使用 RSA-SHA256（RSA2）算法对排序后的参数串验签。
/// </summary>
public sealed class AlipayChannel
{
    private readonly IChannelConfigProvider _configProvider;
    private readonly ILogger<AlipayChannel> _logger;

    public AlipayChannel(
        IChannelConfigProvider configProvider,
        ILogger<AlipayChannel>? logger = null)
    {
        _configProvider = configProvider ?? throw new ArgumentNullException(nameof(configProvider));
        _logger = logger ?? InternalNullLoggerFactory.CreateLogger<AlipayChannel>();
    }

    /// <summary>
    /// 验证支付宝回调签名。
    /// </summary>
    /// <param name="formFields">回调表单字段字典，需包含 sign 字段。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>签名验证结果。</returns>
    public async Task<SignatureVerificationResult> VerifySignatureAsync(
        Dictionary<string, string> formFields, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(formFields);

        var sign = GetField(formFields, "sign");
        if (string.IsNullOrEmpty(sign))
        {
            _logger.LogWarning("支付宝回调验签：缺少 sign 字段");
            return SignatureVerificationResult.Failure("缺少 sign 签名参数");
        }

        var config = await _configProvider.GetConfigAsync(Domain.ValueObjects.PaymentChannel.Alipay, ct)
            .ConfigureAwait(false);

        try
        {
            var verified = Alipay.AlipaySignatureHelper.VerifySign(formFields, config.ApiKey, sign, _logger);

            if (!verified)
            {
                _logger.LogWarning("支付宝回调验签：签名不匹配");
                return SignatureVerificationResult.Failure("签名验证失败");
            }

            _logger.LogInformation("支付宝回调验签通过");
            return SignatureVerificationResult.Success;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // VerifySign 不再吞未预期异常（如 ArgumentNullException 等编程错误），
            // 此处兜底转为 Failure，避免 NotifyController 抛 500。
            _logger.LogError(ex, "支付宝验签未预期异常");
            return SignatureVerificationResult.Failure("验签异常");
        }
    }

    private static string? GetField(Dictionary<string, string> dict, string key)
        => dict.TryGetValue(key, out var v) ? v : null;
}