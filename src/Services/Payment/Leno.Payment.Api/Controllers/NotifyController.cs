using Leno.Payment.Infrastructure.Notify;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.Payment.Api.Controllers;

/// <summary>
/// 支付回调通知控制器。
/// 第三方支付渠道（微信/支付宝）异步通知回调端点，无鉴权（仅验签）。
/// 返回渠道要求的响应格式：微信返回 SUCCESS XML，支付宝返回 success 字符串。
/// </summary>
[ApiController]
[AllowAnonymous]
public sealed class NotifyController : ControllerBase
{
    private readonly WeChatPayNotifyHandler _weChatPayNotifyHandler;
    private readonly AlipayNotifyHandler _alipayNotifyHandler;

    public NotifyController(WeChatPayNotifyHandler weChatPayNotifyHandler, AlipayNotifyHandler alipayNotifyHandler)
    {
        ArgumentNullException.ThrowIfNull(weChatPayNotifyHandler);
        ArgumentNullException.ThrowIfNull(alipayNotifyHandler);
        _weChatPayNotifyHandler = weChatPayNotifyHandler;
        _alipayNotifyHandler = alipayNotifyHandler;
    }

    /// <summary>
    /// 微信支付异步通知回调。
    /// 微信以 POST XML 方式回调，验签通过后返回 <c>&lt;xml&gt;&lt;return_code&gt;SUCCESS&lt;/return_code&gt;&lt;/xml&gt;</c>。
    /// </summary>
    [HttpPost("api/notify/wechat-pay")]
    [Consumes("application/xml", "text/xml", "text/plain")]
    [Produces("application/xml")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    public async Task<IActionResult> WeChatPayNotifyAsync(CancellationToken ct)
    {
        Request.EnableBuffering();
        var rawBody = await new StreamReader(Request.Body).ReadToEndAsync(ct);
        Request.Body.Position = 0;

        var headers = new Dictionary<string, string>();
        foreach (var header in Request.Headers)
        {
            headers[header.Key] = header.Value.ToString();
        }

        var result = await _weChatPayNotifyHandler.HandleAsync(rawBody, headers);
        return Ok(result);
    }

    /// <summary>
    /// 支付宝异步通知回调。
    /// 支付宝以 POST form-urlencoded 方式回调，验签通过后返回 <c>success</c>。
    /// </summary>
    [HttpPost("api/notify/alipay")]
    [Consumes("application/x-www-form-urlencoded")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    public async Task<IActionResult> AlipayNotifyAsync(CancellationToken ct)
    {
        Request.EnableBuffering();
        var rawBody = await new StreamReader(Request.Body).ReadToEndAsync(ct);
        Request.Body.Position = 0;

        var form = await Request.ReadFormAsync(ct);
        var formFields = new Dictionary<string, string>();
        foreach (var field in form)
        {
            formFields[field.Key] = field.Value.ToString();
        }

        var result = await _alipayNotifyHandler.HandleAsync(rawBody, formFields);
        return Ok(result);
    }
}
