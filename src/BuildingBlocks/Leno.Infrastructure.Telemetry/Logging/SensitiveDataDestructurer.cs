using System.Globalization;
using System.Text;

namespace Leno.Infrastructure.Logging;

/// <summary>
/// 敏感数据脱敏工具，对手机号、邮箱、身份证号、银行卡号、密钥、Token 等做日志脱敏。
/// 供 Serilog Destructure 与业务日志调用复用。
/// </summary>
public static class SensitiveDataDestructurer
{
    private const string Mask = "***";

    /// <summary>手机号脱敏：保留前 3 后 4，形如 138****1234。</summary>
    public static string MaskPhone(string? phone)
    {
        if (string.IsNullOrEmpty(phone) || phone.Length < 7)
        {
            return Mask;
        }

        return string.Concat(phone.AsSpan(0, 3), "****", phone.AsSpan(phone.Length - 4));
    }

    /// <summary>邮箱脱敏：用户名保留前 2 位 + ***，域名保留。</summary>
    public static string MaskEmail(string? email)
    {
        if (string.IsNullOrEmpty(email))
        {
            return Mask;
        }

        var atIndex = email.IndexOf('@');
        if (atIndex <= 0)
        {
            return Mask;
        }

        var name = email[..atIndex];
        var domain = email[(atIndex + 1)..];
        var maskedName = name.Length <= 2
            ? string.Concat(name.AsSpan(0, 1), Mask)
            : string.Concat(name.AsSpan(0, 2), Mask);

        return string.Concat(maskedName, "@", domain);
    }

    /// <summary>身份证号脱敏：保留前 4 后 4，中间以 8 个 * 替换。</summary>
    public static string MaskIdCard(string? idCard)
    {
        if (string.IsNullOrEmpty(idCard) || idCard.Length < 10)
        {
            return Mask;
        }

        return string.Concat(idCard.AsSpan(0, 4), "********", idCard.AsSpan(idCard.Length - 4));
    }

    /// <summary>银行卡号脱敏：保留前 4 后 4，中间以 4 个 * 替换。</summary>
    public static string MaskCardNo(string? cardNo)
    {
        if (string.IsNullOrEmpty(cardNo) || cardNo.Length < 8)
        {
            return Mask;
        }

        return string.Concat(cardNo.AsSpan(0, 4), "****", cardNo.AsSpan(cardNo.Length - 4));
    }

    /// <summary>Token / API Key / 密钥脱敏：仅保留前 4 位与后 2 位，中间以 *** 替换。</summary>
    public static string MaskSecret(string? secret)
    {
        if (string.IsNullOrEmpty(secret))
        {
            return Mask;
        }

        if (secret.Length <= 6)
        {
            return Mask;
        }

        return string.Concat(secret.AsSpan(0, 4), Mask, secret.AsSpan(secret.Length - 2));
    }

    /// <summary>金额脱敏：返回固定占位，避免敏感金额明文落日志。</summary>
    public static string MaskAmount(decimal amount)
    {
        _ = amount;
        return Mask;
    }

    /// <summary>
    /// 对任意字符串做通用脱敏：保留前 <paramref name="keepPrefix"/> 与后 <paramref name="keepSuffix"/> 位。
    /// 不足长度时返回 <see cref="Mask"/>。
    /// </summary>
    public static string MaskGeneric(string? value, int keepPrefix, int keepSuffix)
    {
        if (string.IsNullOrEmpty(value))
        {
            return Mask;
        }

        if (value.Length <= keepPrefix + keepSuffix)
        {
            return Mask;
        }

        var sb = new StringBuilder();
        sb.Append(value.AsSpan(0, keepPrefix));
        sb.Append(Mask);
        sb.Append(value.AsSpan(value.Length - keepSuffix));
        return sb.ToString();
    }

    /// <summary>将脱敏字段名转换为结构化日志参数键（大写下划线）。</summary>
    public static string ToLogKey(string fieldName)
    {
        if (string.IsNullOrEmpty(fieldName))
        {
            return fieldName;
        }

        var sb = new StringBuilder(fieldName.Length);
        for (var i = 0; i < fieldName.Length; i++)
        {
            var c = fieldName[i];
            if (char.IsUpper(c) && i > 0)
            {
                sb.Append('_');
            }
            sb.Append(char.ToUpper(c, CultureInfo.InvariantCulture));
        }
        return sb.ToString();
    }
}
