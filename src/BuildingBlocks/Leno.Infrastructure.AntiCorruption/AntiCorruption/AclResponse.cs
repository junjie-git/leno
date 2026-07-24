using System.Collections.ObjectModel;

namespace Leno.Infrastructure.AntiCorruption;

/// <summary>
/// ACL 防腐层响应模型（阶段四 4.2：可插拔策略链）。
/// <para>
/// 调度器 (<see cref="AntiCorruptionDispatcher"/>) 与通道 (<see cref="IAclChannel"/>)
/// 之间的统一返回类型：<c>Success</c> 区分调用成功与业务失败，
/// <c>Body</c> 携带结果键值对，<c>ErrorCode/ErrorMessage</c> 描述业务错误。
/// </para>
/// <para>
/// 通道抛 <see cref="AclChannelException"/> 表示基础设施层失败（网络故障、超时等），
/// 由调度器尝试下一通道；返回 <c>Success=false</c> 的 <see cref="AclResponse"/>
/// 表示业务层失败（如商品不存在、库存不足），调度器不再降级。
/// </para>
/// </summary>
public sealed record AclResponse
{
    /// <summary>是否调用成功（业务成功）。</summary>
    public bool Success { get; init; }

    /// <summary>响应 Body 键值对；成功时填充，失败时可为 null。</summary>
    public IReadOnlyDictionary<string, object>? Body { get; init; }

    /// <summary>业务错误码（如 "PRODUCT_NOT_FOUND"），仅当 <see cref="Success"/>=false 时有值。</summary>
    public string? ErrorCode { get; init; }

    /// <summary>业务错误消息，仅当 <see cref="Success"/>=false 时有值。</summary>
    public string? ErrorMessage { get; init; }

    public AclResponse(
        bool success,
        IReadOnlyDictionary<string, object>? body = null,
        string? errorCode = null,
        string? errorMessage = null)
    {
        Success = success;
        Body = body;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    /// <summary>构造一个成功响应。</summary>
    public static AclResponse Ok(IReadOnlyDictionary<string, object>? body = null)
        => new(success: true, body: body);

    /// <summary>构造一个成功响应，将单个键值对包装为 Body。</summary>
    public static AclResponse Ok(string key, object value)
        => new(success: true, body: new Dictionary<string, object>(1) { [key] = value });

    /// <summary>构造一个失败响应。</summary>
    public static AclResponse Fail(string errorCode, string errorMessage)
        => new(success: false, body: null, errorCode: errorCode, errorMessage: errorMessage);

    /// <summary>构造一个空 Body 的成功响应（适用于无返回值的操作）。</summary>
    public static AclResponse EmptyOk()
        => new(success: true, body: ReadOnlyDictionary<string, object>.Empty);
}
