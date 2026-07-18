namespace Leno.SharedContracts.Events;

/// <summary>
/// 用户注册成功集成事件，用户域发布。
/// 消费方：积分与会员域（创建积分账户与会员档案、发放新人积分）、消息通知域（欢迎通知）。
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class UserRegisteredEvent : IntegrationEventBase
{
    /// <summary>注册用户标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>用户名。</summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>注册邮箱（OAuth 注册可空）。</summary>
    public string? Email { get; init; }

    /// <summary>注册手机号（OAuth 注册可空）。</summary>
    public string? PhoneNumber { get; init; }

    /// <summary>注册时间（UTC），与 <see cref="IntegrationEventBase.OccurredAt"/> 一致。</summary>
    public DateTime RegisteredAt { get; init; }

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => UserId;

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public UserRegisteredEvent() : base()
    {
    }

    public UserRegisteredEvent(Guid userId, string username, string? email, string? phoneNumber)
        : base()
    {
        UserId = userId;
        Username = username;
        Email = email;
        PhoneNumber = phoneNumber;
        RegisteredAt = OccurredAt;
    }
}
