namespace Leno.SystemAdmin.Domain.ValueObjects;

/// <summary>
/// 限流作用域枚举，决定限流规则的应用范围。
/// </summary>
public enum LimitScope
{
    /// <summary>按 IP 地址限流。</summary>
    Ip = 0,

    /// <summary>按用户限流。</summary>
    User = 1,

    /// <summary>全局限流，所有请求共享同一计数器。</summary>
    Global = 2,

    /// <summary>按店铺限流。</summary>
    Shop = 3
}