using System.Security.Cryptography;
using System.Text;
using Leno.Payment.Domain.Services;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Leno.Payment.Infrastructure.Channels;

/// <summary>
/// 微信支付回调签名验证结果。
/// </summary>
public sealed class SignatureVerificationResult
{
    /// <summary>签名验证是否通过。</summary>
    public bool IsValid { get; init; }

    /// <summary>失败原因（验签不通过时）。</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>验证通过时返回。</summary>
    public static SignatureVerificationResult Success => new() { IsValid = true };

    /// <summary>创建失败结果。</summary>
    public static SignatureVerificationResult Failure(string message) => new() { IsValid = false, ErrorMessage = message };
}

/// <summary>
/// 微信支付渠道，封装微信支付回调签名验证逻辑。
/// 验证 Wechatpay-Signature 头、时间戳（5 分钟容差）、随机数（防重放）。
/// </summary>
public sealed class WeChatPayChannel
{
    private readonly IChannelConfigProvider _configProvider;
    private readonly IConnectionMultiplexer? _redis;
    private readonly ILogger<WeChatPayChannel> _logger;

    /// <summary>时间戳容差（秒），默认 300 秒（5 分钟）。</summary>
    private const int TimestampToleranceSeconds = 300;

    public WeChatPayChannel(
        IChannelConfigProvider configProvider,
        IConnectionMultiplexer? redis = null,
        ILogger<WeChatPayChannel>? logger = null)
    {
        _configProvider = configProvider ?? throw new ArgumentNullException(nameof(configProvider));
        _redis = redis;
        _logger = logger ?? InternalNullLoggerFactory.CreateLogger<WeChatPayChannel>();
    }

    /// <summary>
    /// 验证微信支付回调签名。
    /// </summary>
    /// <param name="headers">回调请求头字典，需包含 Wechatpay-Timestamp、Wechatpay-Nonce、Wechatpay-Signature、Wechatpay-Serial。</param>
    /// <param name="rawBody">原始回调请求体 JSON。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>签名验证结果。</returns>
    public async Task<SignatureVerificationResult> VerifySignatureAsync(
        Dictionary<string, string> headers, string rawBody, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(rawBody);

        var timestamp = GetHeader(headers, "Wechatpay-Timestamp");
        var nonce = GetHeader(headers, "Wechatpay-Nonce");
        var signature = GetHeader(headers, "Wechatpay-Signature");
        var serialNo = GetHeader(headers, "Wechatpay-Serial");

        // 1. 验证必填头
        if (string.IsNullOrEmpty(timestamp))
        {
            _logger.LogWarning("微信支付回调验签：缺少 Wechatpay-Timestamp 头");
            return SignatureVerificationResult.Failure("缺少 Wechatpay-Timestamp 请求头");
        }

        if (string.IsNullOrEmpty(nonce))
        {
            _logger.LogWarning("微信支付回调验签：缺少 Wechatpay-Nonce 头");
            return SignatureVerificationResult.Failure("缺少 Wechatpay-Nonce 请求头");
        }

        if (string.IsNullOrEmpty(signature))
        {
            _logger.LogWarning("微信支付回调验签：缺少 Wechatpay-Signature 头");
            return SignatureVerificationResult.Failure("缺少 Wechatpay-Signature 请求头");
        }

        // 2. 验证时间戳（5 分钟容差）
        if (!ValidateTimestamp(timestamp))
        {
            _logger.LogWarning("微信支付回调验签：时间戳超出容差范围 Timestamp={Timestamp}", timestamp);
            return SignatureVerificationResult.Failure("时间戳超出容差范围（5 分钟）");
        }

        try
        {
            // 3. 验证随机数（防重放）
            if (!await ValidateNonceAsync(nonce, ct).ConfigureAwait(false))
            {
                _logger.LogWarning("微信支付回调验签：随机数重复 Nonce={Nonce}", nonce);
                return SignatureVerificationResult.Failure("随机数重复，疑似重放攻击");
            }

            // 4. 验证签名
            var config = await _configProvider.GetConfigAsync(Domain.ValueObjects.PaymentChannel.WeChatPay, ct)
                .ConfigureAwait(false);

            var verified = WeChatPay.WeChatPayV3SignatureHelper.VerifyNotifySign(
                timestamp, nonce, rawBody, signature, config.ApiKey);

            if (!verified)
            {
                _logger.LogWarning("微信支付回调验签：签名不匹配");
                return SignatureVerificationResult.Failure("签名验证失败");
            }

            _logger.LogInformation("微信支付回调验签通过 SerialNo={SerialNo}", serialNo);
            return SignatureVerificationResult.Success;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // fail-closed：防重放/验签过程异常时拒绝验签，让微信重试回调
            _logger.LogError(ex, "微信支付回调验签异常 Nonce={Nonce}", nonce);
            return SignatureVerificationResult.Failure("验签异常");
        }
    }

    /// <summary>
    /// 验证时间戳是否在容差范围内。
    /// </summary>
    private static bool ValidateTimestamp(string timestampStr)
    {
        if (!long.TryParse(timestampStr, out var timestamp))
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var diff = Math.Abs(now - timestamp);
        return diff <= TimestampToleranceSeconds;
    }

    /// <summary>
    /// 验证随机数是否已使用（防重放）。
    /// 使用 Redis SET NX 原子操作，首次写入成功表示未重放。
    /// <see cref="_redis"/> 为 null（开发环境未配置 Redis）时放行，此为配置选择而非故障。
    /// Redis 故障时 fail-closed：向上抛出由 <see cref="VerifySignatureAsync"/> 外层 catch 返回
    /// <see cref="SignatureVerificationResult.Failure"/>，让微信重试回调，对齐 T19 模式。
    /// </summary>
    private async Task<bool> ValidateNonceAsync(string nonce, CancellationToken ct)
    {
        if (_redis is null)
        {
            // Redis 未配置：保持兼容放行，但记警告（生产环境应配置 Redis）
            _logger.LogWarning("微信支付回调：Redis 未配置，跳过防重放检查 Nonce={Nonce}", nonce);
            return true;
        }

        try
        {
            var db = _redis.GetDatabase();
            var key = $"wechatpay:nonce:{nonce}";
            var ttl = TimeSpan.FromSeconds(TimestampToleranceSeconds * 2);
            return await db.StringSetAsync(key, "1", ttl, When.NotExists).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // fail-closed：Redis 故障时拒绝验签，让微信重试回调
            _logger.LogError(ex, "微信支付回调防重放检查 Redis 故障 Nonce={Nonce}", nonce);
            throw;
        }
    }

    private static string? GetHeader(Dictionary<string, string> headers, string key)
        => headers.TryGetValue(key, out var v) ? v : null;
}

/// <summary>
/// 空日志工厂，用于无日志记录器时的默认实现。
/// </summary>
internal static class InternalNullLoggerFactory
{
    public static ILogger<T> CreateLogger<T>() => Microsoft.Extensions.Logging.Abstractions.NullLogger<T>.Instance;
}