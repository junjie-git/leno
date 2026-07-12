namespace Leno.Payment.Application.DTOs;

/// <summary>
/// 支付渠道配置查询结果 DTO。
/// ConfigValue 脱敏显示（仅显示前 4 字符 + "****"）。
/// </summary>
public sealed class PaymentChannelConfigDto
{
    public Guid Id { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string ConfigName { get; set; } = string.Empty;
    public string ConfigValue { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool Enabled { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// 更新支付渠道配置请求 DTO。
/// </summary>
public sealed class UpdatePaymentChannelConfigDto
{
    public string ConfigValue { get; set; } = string.Empty;
    public string? Description { get; set; }
}