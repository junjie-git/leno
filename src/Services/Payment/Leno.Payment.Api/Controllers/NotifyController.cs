using Leno.Payment.Infrastructure.Channels;
using Leno.Payment.Infrastructure.Notify;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.Payment.Api.Controllers;

/// <summary>
/// 支付回调通知控制器。
/// 第三方支付渠道（微信/支付宝）异步通知回调端点，无鉴权（仅验签）。
/// 先验签（401 拒绝），再处理业务逻辑。
/// 返回渠道要求的响应格式：微信返回 SUCCESS/FAIL，支付宝返回 success/fail。
/// </summary>
[ApiController]
[AllowAnonymous]
public sealed class NotifyController : ControllerBase
{
    private readonly WeChatPayNotifyHandler _weChatPayNotifyHandler;
    private readonly AlipayNotifyHandler _alipayNotifyHandler;
    private readonly WeChatPayChannel _weChatPayChannel;
    private readonly AlipayChannel _alipayChannel;

    public NotifyController(
        WeChatPayNotifyHandler weChatPayNotifyHandler,
        AlipayNotifyHandler alipayNotifyHandler,
        WeChatPayChannel weChatPayChannel,
        AlipayChannel alipayChannel)
    {
        ArgumentNullException.ThrowIfNull(weChatPayNotifyHandler);
        ArgumentNullException.ThrowIfNull(alipayNotifyHandler);
        ArgumentNullException.ThrowIfNull(weChatPayChannel);
        ArgumentNullException.ThrowIfNull(alipayChannel);
        _weChatPayNotifyHandler = weChatPayNotifyHandler;
        _alipayNotifyHandler = alipayNotifyHandler;
        _weChatPayChannel = weChatPayChannel;
        _alipayChannel = alipayChannel;
    }

    /// <summary>
    /// 微信支付异步通知回调。
    /// 微信以 POST JSON 方式回调，先验签（401 拒绝），再处理业务。
    /// 验签通过后返回 <c>SUCCESS</c> 或 <c>FAIL</c>。
    /// </summary>
    [HttpPost("api/notify/wechat-pay")]
    [Consumes("application/json", "text/plain")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> WeChatPayNotifyAsync(CancellationToken ct)
    {
        Request.EnableBuffering();
        // P2-17：StreamReader 有内部缓冲区，需 using 显式释放，避免 GC 延迟回收导致缓冲区残留。
        using var reader = new StreamReader(Request.Body);
        var rawBody = await reader.ReadToEndAsync(ct);
        Request.Body.Position = 0;

        var headers = new Dictionary<string, string>();
        foreach (var header in Request.Headers)
        {
            headers[header.Key] = header.Value.ToString();
        }

        // 先验签，失败返回 401
        var verifyResult = await _weChatPayChannel.VerifySignatureAsync(headers, rawBody, ct);
        if (!verifyResult.IsValid)
        {
            return Unauthorized(new { error = "SIGNATURE_VERIFICATION_FAILED", message = verifyResult.ErrorMessage });
        }

        // 验签通过后处理业务逻辑
        var result = await _weChatPayNotifyHandler.HandleAsync(rawBody, headers);
        return Ok(result);
    }

    /// <summary>
    /// 支付宝异步通知回调。
    /// 支付宝以 POST form-urlencoded 方式回调，先验签（401 拒绝），再处理业务。
    /// 验签通过后返回 <c>success</c> 或 <c>fail</c>。
    /// </summary>
    [HttpPost("api/notify/alipay")]
    [Consumes("application/x-www-form-urlencoded")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> AlipayNotifyAsync(CancellationToken ct)
    {
        Request.EnableBuffering();
        // P2-17：StreamReader 有内部缓冲区，需 using 显式释放，避免 GC 延迟回收导致缓冲区残留。
        using var reader = new StreamReader(Request.Body);
        var rawBody = await reader.ReadToEndAsync(ct);
        Request.Body.Position = 0;

        var form = await Request.ReadFormAsync(ct);
        var formFields = new Dictionary<string, string>();
        foreach (var field in form)
        {
            formFields[field.Key] = field.Value.ToString();
        }

        // 先验签，失败返回 401
        var verifyResult = await _alipayChannel.VerifySignatureAsync(formFields, ct);
        if (!verifyResult.IsValid)
        {
            return Unauthorized(new { error = "SIGNATURE_VERIFICATION_FAILED", message = verifyResult.ErrorMessage });
        }

        // 验签通过后处理业务逻辑
        var result = await _alipayNotifyHandler.HandleAsync(rawBody, formFields);
        return Ok(result);
    }
}
