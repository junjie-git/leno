using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Exceptions;
using Leno.SystemAdmin.Domain.ValueObjects;

namespace Leno.SystemAdmin.Domain.Tests;

public class DeadLetterMessageTests
{
    private static readonly Guid ValidMessageId = Guid.NewGuid();
    private const string ValidOriginalMessageId = "MSG-001";
    private const string ValidSourceContext = "OrderService";
    private const string ValidOriginalTopic = "order.created";
    private const string ValidPayload = "{\"orderId\":\"123\"}";
    private const string ValidHeaders = "{\"correlationId\":\"abc\"}";
    private const string ValidErrorReason = "Message processing failed after 5 retries";

    #region Create - Happy Path

    [Fact]
    public void Create_WithValidParameters_ShouldSetAllProperties()
    {
        var message = DeadLetterMessage.Create(
            ValidMessageId, ValidOriginalMessageId, ValidSourceContext,
            ValidOriginalTopic, ValidPayload, ValidHeaders, ValidErrorReason);

        message.MessageId.Should().Be(ValidMessageId);
        message.Id.Should().Be(ValidMessageId);
        message.OriginalMessageId.Should().Be(ValidOriginalMessageId);
        message.SourceContext.Should().Be(ValidSourceContext);
        message.OriginalTopic.Should().Be(ValidOriginalTopic);
        message.Payload.Should().Be(ValidPayload);
        message.Headers.Should().Be(ValidHeaders);
        message.ErrorReason.Should().Be(ValidErrorReason);
        message.Status.Should().Be(DeadLetterStatus.Pending);
        message.OperatorId.Should().BeNull();
        message.DiscardReason.Should().BeNull();
        message.OccurredAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        message.ProcessedAt.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldTrimFields()
    {
        var message = DeadLetterMessage.Create(
            ValidMessageId,
            "  " + ValidOriginalMessageId + "  ",
            "  " + ValidSourceContext + "  ",
            "  " + ValidOriginalTopic + "  ",
            ValidPayload,
            ValidHeaders,
            ValidErrorReason);

        message.OriginalMessageId.Should().Be(ValidOriginalMessageId);
        message.SourceContext.Should().Be(ValidSourceContext);
        message.OriginalTopic.Should().Be(ValidOriginalTopic);
    }

    #endregion

    #region Create - Validation

    [Fact]
    public void Create_WithEmptyMessageId_ShouldThrowDeadLetterIdEmpty()
    {
        var act = () => DeadLetterMessage.Create(
            Guid.Empty, ValidOriginalMessageId, ValidSourceContext,
            ValidOriginalTopic, ValidPayload, ValidHeaders, ValidErrorReason);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("DEAD_LETTER_ID_EMPTY");
    }

    [Fact]
    public void Create_WithNullOriginalMessageId_ShouldThrowOriginalMessageIdEmpty()
    {
        var act = () => DeadLetterMessage.Create(
            ValidMessageId, null!, ValidSourceContext,
            ValidOriginalTopic, ValidPayload, ValidHeaders, ValidErrorReason);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("DEAD_LETTER_ORIGINAL_MESSAGE_ID_EMPTY");
    }

    [Fact]
    public void Create_WithEmptyOriginalMessageId_ShouldThrowOriginalMessageIdEmpty()
    {
        var act = () => DeadLetterMessage.Create(
            ValidMessageId, "", ValidSourceContext,
            ValidOriginalTopic, ValidPayload, ValidHeaders, ValidErrorReason);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("DEAD_LETTER_ORIGINAL_MESSAGE_ID_EMPTY");
    }

    [Fact]
    public void Create_WithNullSourceContext_ShouldThrowSourceContextEmpty()
    {
        var act = () => DeadLetterMessage.Create(
            ValidMessageId, ValidOriginalMessageId, null!,
            ValidOriginalTopic, ValidPayload, ValidHeaders, ValidErrorReason);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("DEAD_LETTER_SOURCE_CONTEXT_EMPTY");
    }

    [Fact]
    public void Create_WithEmptySourceContext_ShouldThrowSourceContextEmpty()
    {
        var act = () => DeadLetterMessage.Create(
            ValidMessageId, ValidOriginalMessageId, "",
            ValidOriginalTopic, ValidPayload, ValidHeaders, ValidErrorReason);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("DEAD_LETTER_SOURCE_CONTEXT_EMPTY");
    }

    [Fact]
    public void Create_WithSourceContextTooLong_ShouldThrowSourceContextLength()
    {
        var longContext = new string('c', 257);

        var act = () => DeadLetterMessage.Create(
            ValidMessageId, ValidOriginalMessageId, longContext,
            ValidOriginalTopic, ValidPayload, ValidHeaders, ValidErrorReason);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("DEAD_LETTER_SOURCE_CONTEXT_LENGTH");
    }

    [Fact]
    public void Create_WithSourceContextAtMaxLength_ShouldSucceed()
    {
        var context = new string('c', 256);

        var message = DeadLetterMessage.Create(
            ValidMessageId, ValidOriginalMessageId, context,
            ValidOriginalTopic, ValidPayload, ValidHeaders, ValidErrorReason);

        message.SourceContext.Should().Be(context);
    }

    [Fact]
    public void Create_WithNullOriginalTopic_ShouldThrowOriginalTopicEmpty()
    {
        var act = () => DeadLetterMessage.Create(
            ValidMessageId, ValidOriginalMessageId, ValidSourceContext,
            null!, ValidPayload, ValidHeaders, ValidErrorReason);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("DEAD_LETTER_ORIGINAL_TOPIC_EMPTY");
    }

    [Fact]
    public void Create_WithEmptyOriginalTopic_ShouldThrowOriginalTopicEmpty()
    {
        var act = () => DeadLetterMessage.Create(
            ValidMessageId, ValidOriginalMessageId, ValidSourceContext,
            "", ValidPayload, ValidHeaders, ValidErrorReason);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("DEAD_LETTER_ORIGINAL_TOPIC_EMPTY");
    }

    [Fact]
    public void Create_WithOriginalTopicTooLong_ShouldThrowOriginalTopicLength()
    {
        var longTopic = new string('t', 257);

        var act = () => DeadLetterMessage.Create(
            ValidMessageId, ValidOriginalMessageId, ValidSourceContext,
            longTopic, ValidPayload, ValidHeaders, ValidErrorReason);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("DEAD_LETTER_ORIGINAL_TOPIC_LENGTH");
    }

    [Fact]
    public void Create_WithOriginalTopicAtMaxLength_ShouldSucceed()
    {
        var topic = new string('t', 256);

        var message = DeadLetterMessage.Create(
            ValidMessageId, ValidOriginalMessageId, ValidSourceContext,
            topic, ValidPayload, ValidHeaders, ValidErrorReason);

        message.OriginalTopic.Should().Be(topic);
    }

    [Fact]
    public void Create_WithNullPayload_ShouldThrowPayloadEmpty()
    {
        var act = () => DeadLetterMessage.Create(
            ValidMessageId, ValidOriginalMessageId, ValidSourceContext,
            ValidOriginalTopic, null!, ValidHeaders, ValidErrorReason);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("DEAD_LETTER_PAYLOAD_EMPTY");
    }

    [Fact]
    public void Create_WithEmptyPayload_ShouldThrowPayloadEmpty()
    {
        var act = () => DeadLetterMessage.Create(
            ValidMessageId, ValidOriginalMessageId, ValidSourceContext,
            ValidOriginalTopic, "", ValidHeaders, ValidErrorReason);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("DEAD_LETTER_PAYLOAD_EMPTY");
    }

    [Fact]
    public void Create_WithNullHeaders_ShouldThrowHeadersEmpty()
    {
        var act = () => DeadLetterMessage.Create(
            ValidMessageId, ValidOriginalMessageId, ValidSourceContext,
            ValidOriginalTopic, ValidPayload, null!, ValidErrorReason);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("DEAD_LETTER_HEADERS_EMPTY");
    }

    [Fact]
    public void Create_WithEmptyHeaders_ShouldThrowHeadersEmpty()
    {
        var act = () => DeadLetterMessage.Create(
            ValidMessageId, ValidOriginalMessageId, ValidSourceContext,
            ValidOriginalTopic, ValidPayload, "", ValidErrorReason);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("DEAD_LETTER_HEADERS_EMPTY");
    }

    [Fact]
    public void Create_WithNullErrorReason_ShouldThrowErrorReasonEmpty()
    {
        var act = () => DeadLetterMessage.Create(
            ValidMessageId, ValidOriginalMessageId, ValidSourceContext,
            ValidOriginalTopic, ValidPayload, ValidHeaders, null!);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("DEAD_LETTER_ERROR_REASON_EMPTY");
    }

    [Fact]
    public void Create_WithEmptyErrorReason_ShouldThrowErrorReasonEmpty()
    {
        var act = () => DeadLetterMessage.Create(
            ValidMessageId, ValidOriginalMessageId, ValidSourceContext,
            ValidOriginalTopic, ValidPayload, ValidHeaders, "");

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("DEAD_LETTER_ERROR_REASON_EMPTY");
    }

    #endregion

    #region Retry

    [Fact]
    public void Retry_FromPending_ShouldTransitionToRetried()
    {
        var message = CreatePendingMessage();

        message.Retry("operator-1");

        message.Status.Should().Be(DeadLetterStatus.Retried);
        message.OperatorId.Should().Be("operator-1");
        message.ProcessedAt.Should().NotBeNull();
        message.ProcessedAt!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Retry_WhenAlreadyRetried_ShouldBeIdempotent()
    {
        var message = CreatePendingMessage();
        message.Retry("operator-1");
        var firstProcessedAt = message.ProcessedAt;

        // Retry again - should be idempotent
        message.Retry("operator-2");

        message.Status.Should().Be(DeadLetterStatus.Retried);
        message.OperatorId.Should().Be("operator-1"); // Should not change
        message.ProcessedAt.Should().Be(firstProcessedAt); // Should not change
    }

    [Fact]
    public void Retry_WhenDiscarded_ShouldThrowAlreadyDiscarded()
    {
        var message = CreatePendingMessage();
        message.Discard("operator-1", "Invalid message");

        var act = () => message.Retry("operator-2");

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("DEAD_LETTER_ALREADY_DISCARDED");
    }

    [Fact]
    public void Retry_WithNullOperatorId_ShouldThrowOperatorEmpty()
    {
        var message = CreatePendingMessage();

        var act = () => message.Retry(null!);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("DEAD_LETTER_OPERATOR_EMPTY");
    }

    [Fact]
    public void Retry_WithEmptyOperatorId_ShouldThrowOperatorEmpty()
    {
        var message = CreatePendingMessage();

        var act = () => message.Retry("");

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("DEAD_LETTER_OPERATOR_EMPTY");
    }

    [Fact]
    public void Retry_ShouldTrimOperatorId()
    {
        var message = CreatePendingMessage();

        message.Retry("  operator-1  ");

        message.OperatorId.Should().Be("operator-1");
    }

    #endregion

    #region Discard

    [Fact]
    public void Discard_FromPending_ShouldTransitionToDiscarded()
    {
        var message = CreatePendingMessage();

        message.Discard("operator-1", "Invalid data");

        message.Status.Should().Be(DeadLetterStatus.Discarded);
        message.OperatorId.Should().Be("operator-1");
        message.DiscardReason.Should().Be("Invalid data");
        message.ProcessedAt.Should().NotBeNull();
        message.ProcessedAt!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Discard_WhenAlreadyDiscarded_ShouldThrowAlreadyDiscarded()
    {
        var message = CreatePendingMessage();
        message.Discard("operator-1", "Invalid message");

        var act = () => message.Discard("operator-2", "Another reason");

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("DEAD_LETTER_ALREADY_DISCARDED");
    }

    [Fact]
    public void Discard_WhenRetried_ShouldThrowAlreadyRetried()
    {
        var message = CreatePendingMessage();
        message.Retry("operator-1");

        var act = () => message.Discard("operator-2", "Changed mind");

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("DEAD_LETTER_ALREADY_RETRIED");
    }

    [Fact]
    public void Discard_WithNullOperatorId_ShouldThrowOperatorEmpty()
    {
        var message = CreatePendingMessage();

        var act = () => message.Discard(null!, "Invalid data");

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("DEAD_LETTER_OPERATOR_EMPTY");
    }

    [Fact]
    public void Discard_WithEmptyOperatorId_ShouldThrowOperatorEmpty()
    {
        var message = CreatePendingMessage();

        var act = () => message.Discard("", "Invalid data");

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("DEAD_LETTER_OPERATOR_EMPTY");
    }

    [Fact]
    public void Discard_WithNullReason_ShouldThrowDiscardReasonEmpty()
    {
        var message = CreatePendingMessage();

        var act = () => message.Discard("operator-1", null!);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("DEAD_LETTER_DISCARD_REASON_EMPTY");
    }

    [Fact]
    public void Discard_WithEmptyReason_ShouldThrowDiscardReasonEmpty()
    {
        var message = CreatePendingMessage();

        var act = () => message.Discard("operator-1", "");

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("DEAD_LETTER_DISCARD_REASON_EMPTY");
    }

    [Fact]
    public void Discard_WithReasonTooLong_ShouldThrowDiscardReasonLength()
    {
        var message = CreatePendingMessage();
        var longReason = new string('r', 1001);

        var act = () => message.Discard("operator-1", longReason);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("DEAD_LETTER_DISCARD_REASON_LENGTH");
    }

    [Fact]
    public void Discard_WithReasonAtMaxLength_ShouldSucceed()
    {
        var message = CreatePendingMessage();
        var reason = new string('r', 1000);

        message.Discard("operator-1", reason);

        message.DiscardReason.Should().Be(reason);
    }

    [Fact]
    public void Discard_ShouldTrimOperatorIdAndReason()
    {
        var message = CreatePendingMessage();

        message.Discard("  operator-1  ", "  Invalid data  ");

        message.OperatorId.Should().Be("operator-1");
        message.DiscardReason.Should().Be("Invalid data");
    }

    #endregion

    #region State Transitions

    [Fact]
    public void StateMachine_PendingToRetried_ShouldTransitionCorrectly()
    {
        var message = CreatePendingMessage();
        message.Status.Should().Be(DeadLetterStatus.Pending);

        message.Retry("operator-1");

        message.Status.Should().Be(DeadLetterStatus.Retried);
    }

    [Fact]
    public void StateMachine_PendingToDiscarded_ShouldTransitionCorrectly()
    {
        var message = CreatePendingMessage();
        message.Status.Should().Be(DeadLetterStatus.Pending);

        message.Discard("operator-1", "Invalid message");

        message.Status.Should().Be(DeadLetterStatus.Discarded);
    }

    [Fact]
    public void StateMachine_RetriedToDiscarded_ShouldNotBeAllowed()
    {
        var message = CreatePendingMessage();
        message.Retry("operator-1");

        var act = () => message.Discard("operator-2", "Changed mind");

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("DEAD_LETTER_ALREADY_RETRIED");
    }

    [Fact]
    public void StateMachine_DiscardedToRetried_ShouldNotBeAllowed()
    {
        var message = CreatePendingMessage();
        message.Discard("operator-1", "Invalid message");

        var act = () => message.Retry("operator-2");

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("DEAD_LETTER_ALREADY_DISCARDED");
    }

    #endregion

    private static DeadLetterMessage CreatePendingMessage()
    {
        return DeadLetterMessage.Create(
            ValidMessageId, ValidOriginalMessageId, ValidSourceContext,
            ValidOriginalTopic, ValidPayload, ValidHeaders, ValidErrorReason);
    }
}