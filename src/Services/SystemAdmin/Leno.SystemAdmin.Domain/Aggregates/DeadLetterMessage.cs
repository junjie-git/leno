using Leno.SystemAdmin.Domain.Exceptions;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.SystemAdmin.Domain.Aggregates;

/// <summary>
/// 死信消息聚合根，封装消费失败进入死信队列的消息生命周期。
/// 状态流转：Pending → Retried（重投成功）；Pending → Discarded（人工丢弃）。
/// 聚合标识 <see cref="Entity.Id"/> 即对外 <c>MessageId</c>。
/// </summary>
public sealed class DeadLetterMessage : AggregateRoot
{
    private const int MaxSourceContextLength = 256;
    private const int MaxOriginalTopicLength = 256;
    private const int MaxDiscardReasonLength = 1000;

    /// <summary>聚合标识，等同 <see cref="Entity.Id"/>。</summary>
    public Guid MessageId => Id;

    /// <summary>原始消息标识。</summary>
    public string OriginalMessageId { get; private set; } = string.Empty;

    /// <summary>来源上下文（产生死信的服务/模块）。</summary>
    public string SourceContext { get; private set; } = string.Empty;

    /// <summary>原始消息主题。</summary>
    public string OriginalTopic { get; private set; } = string.Empty;

    /// <summary>消息载荷（JSON）。</summary>
    public string Payload { get; private set; } = string.Empty;

    /// <summary>消息头（JSON）。</summary>
    public string Headers { get; private set; } = string.Empty;

    /// <summary>错误原因。</summary>
    public string ErrorReason { get; private set; } = string.Empty;

    /// <summary>死信状态。</summary>
    public DeadLetterStatus Status { get; private set; }

    /// <summary>操作者标识，可空。</summary>
    public string? OperatorId { get; private set; }

    /// <summary>丢弃原因，可空。</summary>
    public string? DiscardReason { get; private set; }

    /// <summary>死信发生时间（UTC）。</summary>
    public DateTime OccurredAt { get; private set; }

    /// <summary>处理时间（UTC），可空。</summary>
    public DateTime? ProcessedAt { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private DeadLetterMessage() { }

    private DeadLetterMessage(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，校验各字段并构建死信消息，初始状态为 Pending。
    /// </summary>
    /// <param name="id">死信消息标识，由应用层生成。</param>
    /// <param name="originalMessageId">原始消息标识。</param>
    /// <param name="sourceContext">来源上下文。</param>
    /// <param name="originalTopic">原始消息主题。</param>
    /// <param name="payload">消息载荷（JSON）。</param>
    /// <param name="headers">消息头（JSON）。</param>
    /// <param name="errorReason">错误原因。</param>
    public static DeadLetterMessage Create(
        Guid id,
        string originalMessageId,
        string sourceContext,
        string originalTopic,
        string payload,
        string headers,
        string errorReason)
    {
        if (id == Guid.Empty)
        {
            throw new SystemAdminDomainException("死信消息标识不可为空", "DEAD_LETTER_ID_EMPTY");
        }

        ValidateOriginalMessageId(originalMessageId);
        ValidateSourceContext(sourceContext);
        ValidateOriginalTopic(originalTopic);
        ValidatePayload(payload);
        ValidateHeaders(headers);
        ValidateErrorReason(errorReason);

        return new DeadLetterMessage(id)
        {
            OriginalMessageId = originalMessageId.Trim(),
            SourceContext = sourceContext.Trim(),
            OriginalTopic = originalTopic.Trim(),
            Payload = payload,
            Headers = headers,
            ErrorReason = errorReason,
            Status = DeadLetterStatus.Pending,
            OperatorId = null,
            DiscardReason = null,
            OccurredAt = DateTime.UtcNow,
            ProcessedAt = null
        };
    }

    /// <summary>
    /// 重投消息，仅 Pending 态可重投，已重投则幂等返回当前状态。
    /// </summary>
    /// <param name="operatorId">操作者标识。</param>
    public void Retry(string operatorId)
    {
        if (string.IsNullOrWhiteSpace(operatorId))
        {
            throw new SystemAdminDomainException("操作者标识不可为空", "DEAD_LETTER_OPERATOR_EMPTY");
        }

        if (Status == DeadLetterStatus.Retried)
        {
            return;
        }

        if (Status == DeadLetterStatus.Discarded)
        {
            throw new SystemAdminDomainException("已丢弃的死信消息不可重投", "DEAD_LETTER_ALREADY_DISCARDED", 409);
        }

        Status = DeadLetterStatus.Retried;
        OperatorId = operatorId.Trim();
        ProcessedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// 丢弃消息，仅 Pending 态可丢弃，需提供非空丢弃原因。
    /// </summary>
    /// <param name="operatorId">操作者标识。</param>
    /// <param name="reason">丢弃原因。</param>
    public void Discard(string operatorId, string reason)
    {
        if (string.IsNullOrWhiteSpace(operatorId))
        {
            throw new SystemAdminDomainException("操作者标识不可为空", "DEAD_LETTER_OPERATOR_EMPTY");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new SystemAdminDomainException("丢弃原因不可为空", "DEAD_LETTER_DISCARD_REASON_EMPTY");
        }

        if (reason.Trim().Length > MaxDiscardReasonLength)
        {
            throw new SystemAdminDomainException($"丢弃原因长度不可超过 {MaxDiscardReasonLength} 字符", "DEAD_LETTER_DISCARD_REASON_LENGTH");
        }

        if (Status == DeadLetterStatus.Discarded)
        {
            throw new SystemAdminDomainException("死信消息已丢弃，不可重复丢弃", "DEAD_LETTER_ALREADY_DISCARDED", 409);
        }

        if (Status == DeadLetterStatus.Retried)
        {
            throw new SystemAdminDomainException("已重投的死信消息不可丢弃", "DEAD_LETTER_ALREADY_RETRIED", 409);
        }

        Status = DeadLetterStatus.Discarded;
        OperatorId = operatorId.Trim();
        DiscardReason = reason.Trim();
        ProcessedAt = DateTime.UtcNow;
    }

    private static void ValidateOriginalMessageId(string originalMessageId)
    {
        if (string.IsNullOrWhiteSpace(originalMessageId))
        {
            throw new SystemAdminDomainException("原始消息标识不可为空", "DEAD_LETTER_ORIGINAL_MESSAGE_ID_EMPTY");
        }
    }

    private static void ValidateSourceContext(string sourceContext)
    {
        if (string.IsNullOrWhiteSpace(sourceContext))
        {
            throw new SystemAdminDomainException("来源上下文不可为空", "DEAD_LETTER_SOURCE_CONTEXT_EMPTY");
        }

        if (sourceContext.Trim().Length > MaxSourceContextLength)
        {
            throw new SystemAdminDomainException($"来源上下文长度不可超过 {MaxSourceContextLength} 字符", "DEAD_LETTER_SOURCE_CONTEXT_LENGTH");
        }
    }

    private static void ValidateOriginalTopic(string originalTopic)
    {
        if (string.IsNullOrWhiteSpace(originalTopic))
        {
            throw new SystemAdminDomainException("原始消息主题不可为空", "DEAD_LETTER_ORIGINAL_TOPIC_EMPTY");
        }

        if (originalTopic.Trim().Length > MaxOriginalTopicLength)
        {
            throw new SystemAdminDomainException($"原始消息主题长度不可超过 {MaxOriginalTopicLength} 字符", "DEAD_LETTER_ORIGINAL_TOPIC_LENGTH");
        }
    }

    private static void ValidatePayload(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            throw new SystemAdminDomainException("消息载荷不可为空", "DEAD_LETTER_PAYLOAD_EMPTY");
        }
    }

    private static void ValidateHeaders(string headers)
    {
        if (string.IsNullOrWhiteSpace(headers))
        {
            throw new SystemAdminDomainException("消息头不可为空", "DEAD_LETTER_HEADERS_EMPTY");
        }
    }

    private static void ValidateErrorReason(string errorReason)
    {
        if (string.IsNullOrWhiteSpace(errorReason))
        {
            throw new SystemAdminDomainException("错误原因不可为空", "DEAD_LETTER_ERROR_REASON_EMPTY");
        }
    }
}