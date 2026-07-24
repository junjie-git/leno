namespace Leno.SharedContracts.Localization;

/// <summary>
/// 错误码到本地化资源 key 的映射中心（国际化预留扩展位）。
/// <para>
/// 当前阶段 <c>IStringLocalizer</c> 默认注册空实现，错误消息原样透传，
/// 本映射表仅作为扩展位预置。DG-8 决策门通过后，API 响应错误消息经
/// <c>IStringLocalizer[ErrorCodeCatalog.GetResourceKey(errorCode)]</c> 查询多语言资源。
/// </para>
/// <para>
/// 未注册的错误码回退到 <c>generic_error</c> 资源 key，保证调用方始终拿到非空 key。
/// </para>
/// </summary>
public static class ErrorCodeCatalog
{
    /// <summary>未命中映射时的回退资源 key。</summary>
    public const string FallbackResourceKey = "generic_error";

    /// <summary>
    /// 错误码 → 本地化资源 key 映射表。
    /// 初始化后只读，并发读安全；扩展时通过新增初始化项实现。
    /// </summary>
    private static readonly Dictionary<string, string> _map = new(StringComparer.Ordinal)
    {
        // Cart BC
        ["CART_NOT_FOUND"] = "cart_not_found",

        // Order BC
        ["ORDER_TIMEOUT"] = "order_timeout",

        // Payment BC
        ["PAYMENT_FAILED"] = "payment_failed",

        // Notification BC
        ["NOTIFICATION_TEMPLATE_ID_EMPTY"] = "notification_template_id_empty",
        ["NOTIFICATION_TEMPLATE_CODE_EMPTY"] = "notification_template_code_empty",
        ["NOTIFICATION_TEMPLATE_NAME_EMPTY"] = "notification_template_name_empty",
        ["NOTIFICATION_TEMPLATE_CHANNEL_INVALID"] = "notification_template_channel_invalid",
        ["NOTIFICATION_TEMPLATE_SUBJECT_EMPTY"] = "notification_template_subject_empty",
        ["NOTIFICATION_TEMPLATE_BODY_EMPTY"] = "notification_template_body_empty",
        ["NOTIFICATION_TEMPLATE_SMS_CODE_INVALID"] = "notification_template_sms_code_invalid",
        ["NOTIFICATION_VARIABLE_DUPLICATE"] = "notification_variable_duplicate",
        ["NOTIFICATION_VARIABLE_NAME_EMPTY"] = "notification_variable_name_empty",
        ["NOTIFICATION_RECIPIENT_USER_EMPTY"] = "notification_recipient_user_empty",

        // 通知模板国际化扩展位错误码
        ["NOTIFICATION_TEMPLATE_CULTURE_INVALID"] = "notification_template_culture_invalid",
    };

    /// <summary>
    /// 查询错误码对应的本地化资源 key。
    /// 未命中时返回 <see cref="FallbackResourceKey"/>（"generic_error"）。
    /// </summary>
    /// <param name="errorCode">业务错误码（如 "CART_NOT_FOUND"）。</param>
    /// <returns>本地化资源 key（如 "cart_not_found"），未命中返回 "generic_error"。</returns>
    public static string GetResourceKey(string? errorCode)
    {
        if (string.IsNullOrWhiteSpace(errorCode))
        {
            return FallbackResourceKey;
        }

        return _map.TryGetValue(errorCode, out var key) ? key : FallbackResourceKey;
    }

    /// <summary>
    /// 判断错误码是否已注册映射（用于测试与诊断）。
    /// </summary>
    public static bool IsRegistered(string errorCode)
        => !string.IsNullOrWhiteSpace(errorCode) && _map.ContainsKey(errorCode);
}
